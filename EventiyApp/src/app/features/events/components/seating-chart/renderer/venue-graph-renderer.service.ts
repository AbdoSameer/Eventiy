import { Injectable, ElementRef } from '@angular/core';
import * as d3 from 'd3';
import {
  BlockGroup,
  SeatNode,
  SeatStatus,
  StageFloorPit,
  VenueGraphData,
  VenueMode,
} from '../models/venue-graph.interfaces';

export type SeatClickHandler = (seat: SeatNode) => void;
export type PitClickHandler = (pit: StageFloorPit) => void;
export type BlockClickHandler = (block: BlockGroup) => void;
export type HoverHandler = (event: MouseEvent, target: SeatNode | StageFloorPit | BlockGroup | null) => void;

/**
 * Pure D3.js rendering pipeline.
 *
 * Owns the SVG lifecycle: initialization, zoom/pan attachment,
 * full re-render, and surgical per-node vector updates.
 * Never touches Angular state – all interactivity flows through
 * the supplied callbacks.
 */
@Injectable({ providedIn: 'root' })
export class VenueGraphRendererService {
  private svgSelection!: d3.Selection<SVGSVGElement, unknown, any, any>;
  private mainGroup!: d3.Selection<SVGGElement, unknown, any, any>;
  private zoomBehavior!: d3.ZoomBehavior<SVGSVGElement, unknown>;
  private initialized = false;
  private currentMode: VenueMode = 'SPORT';

  /**
   * Wire D3 to the host <svg> element. Idempotent.
   */
  public initializeEngine(
    svgRef: ElementRef<SVGSVGElement>,
    width: number,
    height: number,
    onZoom?: (transform: d3.ZoomTransform) => void
  ): void {
    if (this.initialized) {
      return;
    }

    this.svgSelection = d3
      .select<SVGSVGElement, unknown>(svgRef.nativeElement)
      .attr('viewBox', `0 0 ${width} ${height}`)
      .attr('preserveAspectRatio', 'xMidYMid meet')
      .style('pointer-events', 'all');

    this.mainGroup = this.svgSelection
      .append('g')
      .attr('class', 'venue-canvas-viewport');

    this.zoomBehavior = d3
      .zoom<SVGSVGElement, unknown>()
      .scaleExtent([0.5, 8])
      .extent([
        [0, 0],
        [width, height],
      ])
      .filter((event: any) => {
        // Disable zoom on direct click+drag of interactive nodes
        const target = event.target as SVGElement | null;
        if (target && (target.classList.contains('seat-node') || target.classList.contains('interactive-pit-polygon'))) {
          return !event.button;
        }
        return !event.ctrlKey && !event.button;
      })
      .on('zoom', (event) => {
        this.mainGroup.attr('transform', event.transform.toString());
        if (onZoom) {
          onZoom(event.transform);
        }
      });

    this.svgSelection.call(this.zoomBehavior);
    this.initialized = true;
  }

  /**
   * Full render. Clears the canvas and re-mounts all layers.
   * Used when mode or top-level data changes.
   *
   * NOTE: This method does NOT purge the graph. Callers that
   * switch modes (SPORT ⇄ CONCERT) should call `purgeGraph()`
   * first to flush detached DOM references.
   */
  public renderVenue(
    data: VenueGraphData,
    mode: VenueMode,
    onSeatClick: SeatClickHandler,
    onPitClick: PitClickHandler,
    onBlockClick: BlockClickHandler,
    onHover: HoverHandler,
  ): void {
    this.currentMode = mode;
    this.mainGroup.selectAll('*').remove();

    // 1. Core (pitch / stage) layer
    this.renderCoreCenter(mode);

    // 2. Floor pits (CONCERT only) drawn beneath the seating rings
    if (mode === 'CONCERT' && data.floorPits?.length) {
      this.renderFloorPits(data.floorPits, onPitClick, onHover);
    }

    // 3. Zone / Block / Seat graph
    this.renderSeatingGraph(data, onSeatClick, onBlockClick, onHover);
  }

  /**
   * Garbage-collection preparation routine executed BEFORE a
   * dual-mode switch (SPORT ⇄ CONCERT). This is the critical
   * memory-leak prevention step.
   *
   *   1. Unbind all native D3 mouse/touch listeners so the
   *      closure references can be collected by V8.
   *   2. Call .remove() on every seat / pit node so the SVG
   *      fragment is detached from the live DOM.
   *   3. Drop the cached selection references to break the
   *      final link so the GC can reclaim the heap.
   */
  public purgeGraph(): void {
    if (!this.initialized) return;

    // 1. Unbind all D3 listeners from existing selections
    this.mainGroup
      .selectAll<SVGElement, unknown>('.seat-node, .interactive-pit-polygon, .core-center-node *')
      .on('mouseenter', null)
      .on('mousemove', null)
      .on('mouseleave', null)
      .on('click', null)
      .on('touchstart', null)
      .on('touchend', null);

    // 2. Force detachment of all seat / pit / core vectors
    this.mainGroup.selectAll('*').remove();

    // 3. Drop the local references – final hand-off to the GC
    this.currentMode = 'SPORT';
  }

  /**
   * Apply a single delta WITHOUT touching the rest of the canvas.
   * The viewport transform matrix is preserved because we never
   * re-mount the main <g> wrapper.
   */
  public applyStatusDelta(seatId: string, status: SeatStatus): void {
    if (!this.initialized) return;
    const node = this.mainGroup
      .select<SVGCircleElement>(`#${CSS.escape(seatId)}`);
    if (node.empty()) return;
    node
      .attr('class', `seat-node status-${status.toLowerCase()}`)
      .attr('r', status === 'SELECTED' ? 5.5 : 4.5);
  }

  /**
   * Trigger the visual flash animation for a collision.
   * Adds a temporary class that the CSS keyframe consumes,
   * then strips it on animationend.
   */
  public flashCollision(seatId: string): void {
    if (!this.initialized) return;
    const node = this.mainGroup.select<SVGCircleElement>(`#${CSS.escape(seatId)}`);
    if (node.empty()) return;
    node.classed('seat-collision', true);
    // CSS handles the removal via animationend; but we also
    // schedule a hard remove in case animationend never fires.
    setTimeout(() => node.classed('seat-collision', false), 800);
  }

  /**
   * Centerpiece of the morphing engine. The two modes paint
   * completely different shapes into the same group slot so
   * the transition is just a redraw, not a layout shift.
   */
  private renderCoreCenter(mode: VenueMode): void {
    const coreGroup = this.mainGroup
      .append('g')
      .attr('class', 'core-center-node');

    if (mode === 'SPORT') {
      // Football pitch
      coreGroup
        .append('rect')
        .attr('x', 380)
        .attr('y', 330)
        .attr('width', 240)
        .attr('height', 240)
        .attr('rx', 10)
        .attr('ry', 10)
        .style('fill', 'var(--color-sport-pitch, #4CAF50)')
        .style('stroke', '#FFFFFF')
        .style('stroke-width', 2);

      coreGroup
        .append('circle')
        .attr('cx', 500)
        .attr('cy', 450)
        .attr('r', 40)
        .style('fill', 'none')
        .style('stroke', '#FFFFFF')
        .style('stroke-width', 2);

      coreGroup
        .append('rect')
        .attr('x', 498)
        .attr('y', 330)
        .attr('width', 4)
        .attr('height', 240)
        .style('fill', '#FFFFFF')
        .style('opacity', 0.6);
    } else {
      // Concert stage
      coreGroup
        .append('rect')
        .attr('x', 350)
        .attr('y', 220)
        .attr('width', 300)
        .attr('height', 80)
        .style('fill', 'var(--color-concert-stage, #1F2937)')
        .style('stroke', '#374151')
        .style('stroke-width', 3);

      coreGroup
        .append('text')
        .attr('x', 500)
        .attr('y', 265)
        .attr('text-anchor', 'middle')
        .style('fill', '#FFFFFF')
        .style('font-family', 'sans-serif')
        .style('font-weight', '700')
        .style('font-size', '14px')
        .text('MAIN STAGE');

      // Soft spotlight ellipse
      coreGroup
        .append('ellipse')
        .attr('cx', 500)
        .attr('cy', 450)
        .attr('rx', 160)
        .attr('ry', 100)
        .style('fill', '#F6544C')
        .style('fill-opacity', 0.06);
    }
  }

  private renderFloorPits(
    pits: StageFloorPit[],
    onPitClick: PitClickHandler,
    onHover: HoverHandler,
  ): void {
    const pitGroup = this.mainGroup.append('g').attr('class', 'floor-pits-layer');

    pitGroup
      .selectAll<SVGPolygonElement, StageFloorPit>('polygon')
      .data(pits)
      .enter()
      .append('polygon')
      .attr('points', (d) => d.polygonPoints)
      .attr('class', (d) => `interactive-pit-polygon status-${d.status.toLowerCase()}`)
      .style('fill', (d) => `var(${d.colorToken})`)
      .style('fill-opacity', 0.7)
      .style('stroke', (d) => `var(${d.colorToken})`)
      .style('stroke-width', 2)
      .style('cursor', 'pointer')
      .style('transition', 'fill-opacity 200ms ease, stroke-width 200ms ease')
      .on('mouseenter', function (event, d) {
        d3.select(this).style('fill-opacity', 0.9).style('stroke-width', 3);
        onHover(event, d);
      })
      .on('mousemove', (event, d) => onHover(event, d))
      .on('mouseleave', function (event) {
        d3.select(this).style('fill-opacity', 0.7).style('stroke-width', 2);
        onHover(event, null);
      })
      .on('click', (_event, d) => onPitClick(d));
  }

  private renderSeatingGraph(
    data: VenueGraphData,
    onSeatClick: SeatClickHandler,
    onBlockClick: BlockClickHandler,
    onHover: HoverHandler,
  ): void {
    const zonesGroup = this.mainGroup.append('g').attr('class', 'zones-layer');

    data.zones.forEach((zone) => {
      zone.blocks.forEach((block) => {
        const blockGroup = zonesGroup
          .append('g')
          .attr('class', `block-group-${block.id}`)
          .style('fill', `var(${zone.colorToken})`);

        if (block.boundaryPath) {
          blockGroup
            .append('path')
            .attr('d', block.boundaryPath)
            .attr('class', 'block-boundary')
            .style('fill', `var(${zone.colorToken})`)
            .style('stroke', `var(${zone.colorToken})`)
            .style('stroke-width', 1)
            .style('cursor', 'pointer')
            .on('mouseenter', function (event) {
              d3.select(this).classed('is-highlighted', true);
              onHover(event, block);
            })
            .on('mousemove', (event) => onHover(event, block))
            .on('mouseleave', function (event) {
              d3.select(this).classed('is-highlighted', false);
              onHover(event, null);
            })
            .on('click', (event) => {
              onBlockClick(block);
            });
        }

        blockGroup
          .selectAll<SVGCircleElement, SeatNode>('circle')
          .data(block.seats)
          .enter()
          .append('circle')
          .attr('cx', (d) => d.cx)
          .attr('cy', (d) => d.cy)
          .attr('r', this.currentMode === 'CONCERT' ? 3.5 : 4.5)
          .attr('id', (d) => d.id)
          .attr('class', (d) => `seat-node status-${d.status.toLowerCase()}`)
          .attr('transform', (d) => `rotate(${d.rotationAngle}, ${d.cx}, ${d.cy})`)
          .style('fill', 'currentColor')
          .style('color', `var(${zone.colorToken})`)
          .style('stroke', '#FFFFFF')
          .style('stroke-width', 0.5)
          .style('cursor', 'pointer')
          .style('will-change', 'fill, r, transform')
          .style('transition', 'fill 200ms ease, r 200ms ease')
          .on('mouseenter', function (event, d) {
            // Stop event propagation so block boundary hover doesn't override this
            event.stopPropagation();
            d3.select(this).attr('r', 6.5);
            onHover(event, d);
          })
          .on('mousemove', (event, d) => {
            event.stopPropagation();
            onHover(event, d);
          })
          .on('mouseleave', function (event) {
            event.stopPropagation();
            d3.select(this).attr('r', this.classList.contains('seat-node') && this.classList.contains('status-selected') ? 5.5 : 4.5);
            onHover(event, null);
          })
          .on('click', (event, d) => {
            event.stopPropagation();
            onSeatClick(d);
          });
      });
    });
  }

  /**
   * Surgical update – mutate just one node's class. Avoids
   * re-rendering the entire 1000+ seat graph.
   */
  public updateSeatVisualState(seatId: string, status: SeatStatus): void {
    if (!this.initialized) return;
    this.mainGroup
      .select<SVGCircleElement>(`#${CSS.escape(seatId)}`)
      .attr('class', `seat-node status-${status.toLowerCase()}`)
      .attr('r', status === 'SELECTED' ? 5.5 : 4.5);
  }

  public highlightBlock(blockId: string, highlight: boolean): void {
    if (!this.initialized) return;
    const blockPath = this.mainGroup.select(`.block-group-${CSS.escape(blockId)} .block-boundary`);
    if (blockPath.empty()) return;
    blockPath.classed('is-highlighted', highlight);
  }

  public resetZoom(): void {
    if (!this.initialized) return;
    this.svgSelection
      .transition()
      .duration(500)
      .call(this.zoomBehavior.transform, d3.zoomIdentity);
  }

  /** Returns the current zoom transform for badge clusterization. */
  public getCurrentScale(): number {
    if (!this.initialized) return 1;
    const t = d3.zoomTransform(this.svgSelection.node()!);
    return t.k;
  }
}
