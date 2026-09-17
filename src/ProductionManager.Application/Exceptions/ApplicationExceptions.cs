namespace ProductionManager.Application.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);
public sealed class ConflictException(string message, Exception? inner = null) : Exception(message, inner);
public sealed class ForbiddenException(string message) : Exception(message);
