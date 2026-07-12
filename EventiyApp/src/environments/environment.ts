/**
 * Application environment configuration.
 *
 * The backend is an ASP.NET Core Web API exposed under /api.
 * All Result-pattern responses are wrapped in { isSuccess, isFailure, value?, errors? }.
 */
export const environment = {
  production: false,
  apiUrl: 'https://localhost:7001/api',
  wsUrl: 'wss://localhost:7001',
};
