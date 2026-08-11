/**
 * Error type that carries an HTTP status. Services throw these; the error
 * middleware turns them into responses. Anything else that reaches the
 * middleware is treated as an unexpected 500 and its detail is not leaked.
 */
export class HttpError extends Error {
  readonly status: number;
  readonly code: string;
  readonly details?: unknown;

  constructor(status: number, code: string, message: string, details?: unknown) {
    super(message);
    this.name = 'HttpError';
    this.status = status;
    this.code = code;
    this.details = details;
  }

  static badRequest(message: string, details?: unknown) {
    return new HttpError(400, 'BAD_REQUEST', message, details);
  }

  static unauthorized(message = 'Authentication required.') {
    return new HttpError(401, 'UNAUTHORIZED', message);
  }

  static forbidden(message = 'You do not have access to this resource.') {
    return new HttpError(403, 'FORBIDDEN', message);
  }

  static notFound(message = 'Resource not found.') {
    return new HttpError(404, 'NOT_FOUND', message);
  }

  static conflict(message: string) {
    return new HttpError(409, 'CONFLICT', message);
  }

  static unprocessable(message: string, details?: unknown) {
    return new HttpError(422, 'UNPROCESSABLE', message, details);
  }

  static serviceUnavailable(message: string) {
    return new HttpError(503, 'SERVICE_UNAVAILABLE', message);
  }
}
