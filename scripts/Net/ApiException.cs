using System;

namespace Loopholes.Client.Net;

/// <summary>
/// A call the server refused, or one that never got there.
///
/// <see cref="Code"/> is the server's stable machine-readable reason
/// (<c>insufficient_energy</c>, <c>under_investigation</c>, <c>job_locked</c>…),
/// so UI can branch on it. Transport failures use <see cref="TransportCode"/>
/// with a status of 0, which is the distinction that matters on mobile: "the
/// server said no" and "the train went into a tunnel" need different handling.
/// </summary>
public sealed class ApiException(string code, string message, int status = 0) : Exception(message)
{
    public const string TransportCode = "transport";

    public string Code { get; } = code;

    public int Status { get; } = status;

    /// <summary>True when the request never reached the server, so retrying may work.</summary>
    public bool IsTransport => Code == TransportCode;

    /// <summary>True when the token is missing or no longer valid and the player must re-register.</summary>
    public bool IsUnauthorized => Status == 401;
}
