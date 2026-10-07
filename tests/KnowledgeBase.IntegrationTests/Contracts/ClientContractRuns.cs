using KnowledgeBase.Cli.Clients;
using KnowledgeBase.Cli.Models;
using KnowledgeBase.IntegrationTests.Support;

namespace KnowledgeBase.IntegrationTests.Contracts;

// Chạy bộ contract test (K01–K06) với MockKbClient: dữ liệu nằm trong bộ nhớ.
public class MockKbClientContractTests : KbClientContractTests
{
    protected override Task<IKbClient> CreateClientAsync(IReadOnlyList<KbDocument> seed)
        => Task.FromResult<IKbClient>(new MockKbClient(seed));
}

// Chạy CÙNG bộ contract test với HttpKbClient: request đi qua mạng thật (Kestrel, 127.0.0.1) tới
// KnowledgeBase.Api. Pass cả hai lớp này = HTTP client có cùng hành vi với mock.
public class HttpKbClientContractTests : KbClientContractTests
{
    private RunningKbApi? _server;

    protected override async Task<IKbClient> CreateClientAsync(IReadOnlyList<KbDocument> seed)
    {
        _server = await RunningKbApi.StartAsync(new MockKbClient(seed));
        return new HttpKbClient(new HttpClient { BaseAddress = new Uri(_server.Url) });
    }

    protected override async Task CleanUpAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
    }
}
