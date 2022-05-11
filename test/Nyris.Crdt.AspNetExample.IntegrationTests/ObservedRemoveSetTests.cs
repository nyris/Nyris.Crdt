using FluentAssertions;
using Grpc.Net.Client;
using Nyris.Crdt.AspNetExample;
using Nyris.Crdt.GrpcServiceSample.IntegrationTests.Utils;
using Nyris.Extensions.Guids;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nyris.Crdt.GrpcServiceSample.IntegrationTests;

public class ObservedRemoveSetTests : IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly GrpcChannel _channelA;
    private readonly Api.ApiClient _clientA;

    private readonly GrpcChannel _channelB;
    private readonly Api.ApiClient _clientB;

    private readonly GrpcChannel _channelC;
    private readonly Api.ApiClient _clientC;

    public ObservedRemoveSetTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        _channelA = GrpcChannel.ForAddress("http://localhost:5001");
        _clientA = new Api.ApiClient(_channelA);
        _channelB = GrpcChannel.ForAddress("http://localhost:5011");
        _clientB = new Api.ApiClient(_channelB);
        _channelC = GrpcChannel.ForAddress("http://localhost:5021");
        _clientC = new Api.ApiClient(_channelC);
    }

    /// <summary>
    /// Tests that all instances are available and able to provide a gRPC api on expected ports.
    /// </summary>
    [Fact]
    public async Task GreetingsTest()
    {
        await Task.WhenAll(TestClient(_clientA), TestClient(_clientB), TestClient(_clientC));

        async Task TestClient(Api.ApiClient client)
        {
            const string name = "Integration Test";
            var response = await client.SayHelloAsync(new HelloRequest { Name = name });
            response.Message.Should().Be($"Hello {name}");
        }
    }

    [Fact]
    public async Task CreateItemWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();
        _testOutputHelper.WriteLine("TraceId: " + traceId);

        var users = await AddRandomUsersAsync(_clientA, 1, traceId);

        var retrievedUser = await _clientA.GetUserAsync(new UserGetRequest
        {
            Id = users[0].Guid,
            TraceId = traceId
        });

        retrievedUser.Should().BeEquivalentTo(users[0]);

        // NOTE: Clean up
        await _clientA.DeleteUserAsync(new UserDeleteRequest
        {
            Id = users[0].Guid,
            TraceId = traceId
        });
    }

    [Fact]
    public async Task PropagateItemsWorks()
    {
        var traceId = ShortGuid.NewGuid().ToString();

        try
        {
            _testOutputHelper.WriteLine("TraceId: " + traceId);

            var resultA = await AddRandomUsersAsync(_clientA, 4, traceId);
            var resultB = await AddRandomUsersAsync(_clientB, 4, traceId);
            var resultC = await AddRandomUsersAsync(_clientC, 2, traceId);

            var result = resultA.Concat(resultB).Concat(resultC).OrderBy(u => u.FirstName).ToList();

            await TestingUtils.WaitFor(async () =>
            {
                var usersResponseA = await _clientA.GetAllUsersAsync(new UsersGetRequest { TraceId = traceId });
                usersResponseA.Users.OrderBy(u => u.FirstName).Should().BeEquivalentTo(result);
            });
            await TestingUtils.WaitFor(async () =>
            {
                var usersResponseB = await _clientB.GetAllUsersAsync(new UsersGetRequest { TraceId = traceId });
                usersResponseB.Users.OrderBy(u => u.FirstName).Should().BeEquivalentTo(result);
            });
            await TestingUtils.WaitFor(async () =>
            {
                var usersResponseC = await _clientC.GetAllUsersAsync(new UsersGetRequest { TraceId = traceId });
                usersResponseC.Users.OrderBy(u => u.FirstName).Should().BeEquivalentTo(result);
            });
        }
        finally
        {
            // NOTE: Clean up
            await _clientA.DeleteAllUsersAsync(new UsersDeleteRequest { TraceId = traceId });
            await _clientB.DeleteAllUsersAsync(new UsersDeleteRequest { TraceId = traceId });
            await _clientC.DeleteAllUsersAsync(new UsersDeleteRequest { TraceId = traceId });
        }
    }

    /// <summary>
    /// Test to verify that A Node, after it receives the A Delta from another Node, calculates a new Delta based on it's current state
    /// <para />
    /// <a href="https://vitorenes.org/publication/enes-efficient-synchronization/enes-efficient-synchronization.pdf">Removing Redundant (RR) Delta Propagation</a>
    /// <para />
    /// NewDelta = LocalState - ReceivedDelta
    /// <para />
    /// </summary>
    [Fact(Skip = "TODO")]
    public void Propagates_Redundant_Removed_Delta() { }

    /// <summary>
    /// Test to verify that A Node does not send Delta back to it's origin while propagating Delta
    /// <para />
    /// <a href="https://vitorenes.org/publication/enes-efficient-synchronization/enes-efficient-synchronization.pdf">Avoid Back Propagation (BP)</a>
    /// <para />
    /// NodeToSendDelta = AllNodes - CurrentNode - OriginNode
    /// <para />
    /// </summary>
    [Fact(Skip = "TODO")]
    public void Propagating_Delta_Should_Avoid_Cycles() { }

    public void Dispose()
    {
        _channelA.Dispose();
        _channelB.Dispose();
        _channelC.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<IList<UserResponse>> AddRandomUsersAsync(Api.ApiClient client, int n, string traceId)
    {
        var responses = new List<UserResponse>(n);

        for (var i = 0; i < n; ++i)
        {
            var userResponse = await client.CreateUserAsync(new UserCreateRequest
            {
                Guid = Guid.NewGuid().ToString(),
                FirstName = "first-" + i,
                LastName = "last-" + i,
                TraceId = traceId
            });

            responses.Add(userResponse);
        }

        return responses;
    }
}
