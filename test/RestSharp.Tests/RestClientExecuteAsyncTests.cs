using Moq;
using RestSharp.Tests.Fixtures;
using RichardSzalay.MockHttp;

namespace RestSharp.Tests;

public class RestClientExecuteAsyncTests {
    [Fact]
    public async Task IRestClient_ExecuteAsync_can_be_mocked_without_serialization() {
        var request  = new RestRequest("resource");
        var expected = new RestResponse<Dummy>(request);

        var client = new Mock<IRestClient>();
        client
            .Setup(c => c.ExecuteAsync<Dummy>(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var actual = await client.Object.ExecuteAsync<Dummy>(request);

        actual.Should().BeSameAs(expected);
        client.Verify(c => c.ExecuteAsync<Dummy>(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestClient_ExecuteAsync_deserializes_the_response() {
        using var client = MockHttpClient.Create(Method.Get, r => r.RespondWithJson());

        var request  = new RestRequest();
        var response = await client.ExecuteAsync<Dummy>(request);

        response.Data.Should().NotBeNull();
        response.Data!.Name.Should().Be("alpha");
    }

    record Dummy(string Name);
}

static class MockedRequestExtensions {
    public static MockedRequest RespondWithJson(this MockedRequest request)
        => request.Respond("application/json", """{"Name":"alpha"}""");
}
