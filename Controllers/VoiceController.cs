using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Twilio.AspNet.Common;
using Twilio.AspNet.Core;
using Twilio.TwiML;
using Twilio.TwiML.Voice;

namespace TwilioNoPickUp.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class VoiceController : TwilioController
    {
        private readonly ILogger<VoiceController> logger;
        private readonly ICallbackService callbackService;
        private readonly HashSet<string> badStatusCodes = new HashSet<string>{
            "busy",
            "no-answer",
            "canceled",
            "failed",
        };

        public VoiceController(
            ILogger<VoiceController> logger,
            ICallbackService callbackService)
        {
            this.logger = logger;
            this.callbackService = callbackService;
        }

        [HttpPost]
        public TwiMLResult Incoming()
        {
            var response = new VoiceResponse();
            var dial = new Dial(action: CreateActionUri(nameof(IncomingAction)));
            dial.Client("NON-EXISTANT-CLIENT");
            response.Append(dial);
            return TwiML(response);
        }

        [HttpPost]
        public TwiMLResult IncomingAction([FromForm] StatusCallbackRequest request)
        {
            var response = new VoiceResponse();
            if (badStatusCodes.Contains(request.DialCallStatus))
            {
                logger.LogInformation("Bad dial call status: {DialCallStatus}", request.DialCallStatus);
                var gather = new Gather(numDigits: 1, action: CreateActionUri(nameof(RequestCallback)));
                gather.Say("The person you are trying to reach is unavailable. If you would like to receive a callback, press 1. If not, press 2 or hang up.");
                response.Append(gather)
                    .Redirect(CreateActionUri(nameof(RequestCallback)));
            }

            return TwiML(response);
        }

        [HttpPost]
        public TwiMLResult RequestCallback([FromForm] VoiceRequest request)
        {
            var response = new VoiceResponse();
            Gather gather;
            switch (request.Digits)
            {
                case "1":
                    gather = new Gather(numDigits: 10, action: CreateActionUri(nameof(CapturePhoneNumber)));
                    gather.Say("Please enter your 10 digit phone number");
                    response.Append(gather)
                        .Redirect(CreateActionUri(nameof(RequestCallback)));
                    break;
                case "2":
                    response.Say("Goodbye!")
                        .Hangup();
                    break;
                default:
                    response.Say("Sorry, I don't understand that choice.")
                        .Pause();

                    gather = new Gather(numDigits: 1, action: CreateActionUri(nameof(RequestCallback)));
                    gather.Say("If you would like to receive a callback, press 1. If not, press 2 or hang up.");
                    response.Append(gather)
                        .Redirect(CreateActionUri(nameof(RequestCallback)));
                    break;
            }

            return TwiML(response);
        }

        [HttpPost]
        public TwiMLResult CapturePhoneNumber([FromForm] VoiceRequest request)
        {
            var response = new VoiceResponse();
            if (request.Digits.Length != 10)
            {
                response.Say($"You entered {request.Digits.Length} digits.")
                    .Pause()
                    .Gather(numDigits: 10, action: CreateActionUri(nameof(RequestCallback)))
                    .Say("Please enter your 10 digit phone number");
                return TwiML(response);
            }
            else
            {
                callbackService.CreateCallback(request.CallSid, request.Digits);
                response.Say("Please let us know what you are calling about by leaving a message after the beep.")
                    .Pause()
                    .Record(
                        action: CreateActionUri(nameof(FinishCall)),
                        timeout: 5,
                        transcribe: true,
                        transcribeCallback: CreateActionUri(nameof(CaptureVoiceMailTranscript))
                    )
                    .Say("Your callback has been requested. Goodbye.")
                    .Hangup();
            }

            return TwiML(response);
        }

        [HttpPost]
        public TwiMLResult FinishCall([FromForm] VoiceRequest request)
        {
            var response = new VoiceResponse();
            response.Say("Your callback has been requested. Goodbye.")
                   .Hangup();
            return TwiML(response);
        }

        [HttpPost]
        public void CaptureVoiceMailTranscript([FromForm] VoiceRequest request)
        {
            callbackService.AddTranscriptToCallback(request.CallSid, request.TranscriptionText);
        }

        private Uri CreateActionUri(string actionName) => new Uri(this.Url.Action(actionName), UriKind.Relative);
    }
}
