namespace KnowledgeBase.Cli.Services;

// Input của người dùng không hợp lệ. Exit code 1.
public class ValidationException(string message) : Exception(message);
