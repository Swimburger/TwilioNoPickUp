public interface ICallbackService
{
    void CreateCallback(string callSid, string phoneNumber);
    void AddTranscriptToCallback(string callSid, string transcript);
}