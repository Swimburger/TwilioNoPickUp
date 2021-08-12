using Microsoft.Extensions.Logging;

public class DummyCrmCallbackService : ICallbackService
{
    private readonly ILogger<DummyCrmCallbackService> logger;

    public DummyCrmCallbackService(ILogger<DummyCrmCallbackService> logger)
    {
        this.logger = logger;
    }

    public void CreateCallback(string callSid, string phoneNumber)
    {
        logger.LogInformation("CreateCallback(callSid: {CallSid}, phoneNumber: {PhoneNumber})", callSid, phoneNumber);
    }

    public void AddTranscriptToCallback(string callSid, string transcript)
    {
        logger.LogInformation("AddTranscriptToCallback(callSid: {CallSid}, transcript: {Transcript})", callSid, transcript);
    }
}
