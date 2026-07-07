/**
 * Generic Result envelope returned by the backend (CQRS Result pattern).
 *
 * Every API response is one of:
 *   Success → { isSuccess: true,  isFailure: false, value: T }
 *   Failure → { isSuccess: false, isFailure: true,  errors: ErrorDetail[] }
 *
 * Services return Observable<Result<T>> so components can branch on isSuccess
 * before touching `value`.
 */
export interface Result<T> {
  isSuccess: boolean;
  isFailure: boolean;
  value?: T;
  errors?: ErrorDetail[];
}

/** Single validation/business error produced by the backend. */
export interface ErrorDetail {
  code: string;
  message: string;
  type: number;
}
