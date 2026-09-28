using Microsoft.AspNetCore.Http;

namespace TestDoubles.Helpers;

public static class HttpContextTestFactory
{
    public static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/test";
        return context;
    }

    /// <summary>
    /// Creates an <see cref="HttpContext"/> whose response body is a real, inspectable
    /// <see cref="MemoryStream"/>, returned alongside the context.
    /// </summary>
    /// <remarks>
    /// <b>Use this whenever a test asserts on the response body.</b> A
    /// <see cref="DefaultHttpContext"/> leaves <c>Response.Body</c> at <c>Stream.Null</c>, so
    /// everything the handler serialises is discarded and the assertions pass *vacuously* — a
    /// <c>DoesNotContain</c> against an empty string is trivially true, and nothing warns you.
    /// <para>
    /// The returned stream is still at position 0 after the handler has written, because
    /// <c>WriteAsJsonAsync</c> leaves the write position at the end. Pass it to
    /// <see cref="ReadBodyAsync"/> rather than reading it directly.
    /// </para>
    /// </remarks>
    public static (HttpContext Context, MemoryStream Body) CreateHttpContextWithReadableBody()
    {
        var context = CreateHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;
        return (context, body);
    }

    /// <summary>
    /// Reads a response body captured by <see cref="CreateHttpContextWithReadableBody"/> back to
    /// the start, then reads it as a string.
    /// </summary>
    /// <param name="body">
    /// The stream returned alongside the context. The reader is disposed without closing it, so
    /// the same stream can be read again.
    /// </param>
    public static async Task<string> ReadBodyAsync(MemoryStream body)
    {
        ArgumentNullException.ThrowIfNull(body);

        body.Position = 0;
        using var reader = new StreamReader(body, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
