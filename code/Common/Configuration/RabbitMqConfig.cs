namespace Common.Configuration;

public sealed record RabbitMqConfig
{
    public required string Host { get; init; }

    public int Port { get; init; } = 5672;

    public string VirtualHost { get; init; } = "/";

    public required string Username { get; init; }

    public required string Password { get; init; }
}
