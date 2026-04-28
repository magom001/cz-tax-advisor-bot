namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Job representing an uploaded document to be processed for data extraction.
/// </summary>
public sealed record DocumentUploadJob(string FileName, string ContentType, byte[] Data);
