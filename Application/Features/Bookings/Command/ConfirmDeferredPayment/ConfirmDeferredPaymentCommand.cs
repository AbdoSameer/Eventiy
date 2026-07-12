using Application.Abstractions.Messaging;

namespace Application.Features.Bookings.Command.ConfirmDeferredPayment;

public sealed record ConfirmDeferredPaymentCommand(string ReferenceCode) : ICommand;
