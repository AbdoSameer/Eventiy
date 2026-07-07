/**
 * Application environment configuration.
 *
 * The backend is an ASP.NET Core Web API exposed under /api.
 * All Result-pattern responses are wrapped in { isSuccess, isFailure, value?, errors? }.
 */
export const environment = {
  production: false,
  /**
   * Base URL of the ASP.NET Core backend.
   * Auth interceptor strips Bearer token only for paths starting with `/auth/`.
   */
  apiUrl: 'https://localhost:7001/api',
};
