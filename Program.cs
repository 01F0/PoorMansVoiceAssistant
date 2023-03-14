using Microsoft.CognitiveServices.Speech;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels;
using OpenAI.GPT3.ObjectModels.RequestModels;

namespace PoorMansVoiceAssistant;

static class Program
{
    public static List<ChatMessage> AllMessages = new()
        {
            ChatMessage.FromSystem("Du är en hjälpsam AI-assistent. Du ger korta och glada svar och skämtar ibland.")
        };

    public static async Task RecognizeSpeechAsync()
    {
        var openAiService = new OpenAIService(new OpenAiOptions
        {
            ApiKey = "REPLACE_ME"
        });

        // Create an Azure Speech Service and put the subscription key here
        var config = SpeechConfig.FromSubscription("REPLACE_ME", "westeurope");
        config.SpeechSynthesisVoiceName = "sv-SE-MattiasNeural";
        config.SetProfanity(ProfanityOption.Removed);
        config.SpeechRecognitionLanguage = "sv-SE";

        using var recognizer = new SpeechRecognizer(config);

        Console.WriteLine("Say something...");

        var sttResult = await recognizer.RecognizeOnceAsync();

        if (sttResult.Reason == ResultReason.RecognizedSpeech)
        {
            Console.WriteLine($"We recognized: {sttResult.Text}");

            var moderationResponse = await openAiService.CreateModeration(new CreateModerationRequest()
            {
                Input = sttResult.Text
            });

            var firstResponse = moderationResponse.Results.FirstOrDefault();

            if (firstResponse == null)
            {
                Console.WriteLine("Create Moderation test failed");
                return;
            }

            if (firstResponse.Flagged)
            {
                Console.WriteLine("Your input was flagged by OpenAI, will not send to OpenAI.");
                return;
            }

            AllMessages.Add(ChatMessage.FromUser(sttResult.Text));

            var completionResult = await openAiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
            {
                Messages = AllMessages,
                Model = Models.ChatGpt3_5Turbo,
                MaxTokens = 300
            });

            if (completionResult.Successful)
            {
                var content = completionResult.Choices.First().Message.Content;
                Console.WriteLine(content);

                AllMessages.Add(ChatMessage.FromAssistance(content));

                using (var synthesizer = new SpeechSynthesizer(config))
                {
                    using (var ttsResult = await synthesizer.SpeakTextAsync(content))
                    {
                        if (ttsResult.Reason == ResultReason.SynthesizingAudioCompleted)
                        {
                            Console.WriteLine($"Speech synthesized to speaker for text [{content}]");
                        }
                        else if (ttsResult.Reason == ResultReason.Canceled)
                        {
                            var cancellation = SpeechSynthesisCancellationDetails.FromResult(ttsResult);
                            Console.WriteLine($"CANCELED: Reason={cancellation.Reason}");

                            if (cancellation.Reason == CancellationReason.Error)
                            {
                                Console.WriteLine($"CANCELED: ErrorCode={cancellation.ErrorCode}");
                                Console.WriteLine($"CANCELED: ErrorDetails=[{cancellation.ErrorDetails}]");
                                Console.WriteLine($"CANCELED: Did you update the subscription info?");
                            }
                        }
                    }
                }
            }


        }
        else if (sttResult.Reason == ResultReason.NoMatch)
        {
            Console.WriteLine("NOMATCH: Speech could not be recognized.");
        }
        else if (sttResult.Reason == ResultReason.Canceled)
        {
            var cancellation = CancellationDetails.FromResult(sttResult);
            Console.WriteLine("CANCELED: Reason={cancellation.Reason}");

            if (cancellation.Reason == CancellationReason.Error)
            {
                Console.WriteLine("CANCELED: ErrorCode={cancellation.ErrorCode}");
                Console.WriteLine("CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                Console.WriteLine("CANCELED: Did you update the subscription info?");
            }
        }
    }

    static async Task Main()
    {
        while (true)
        {
            Console.WriteLine("Please press <Return> to continue.");
            Console.ReadLine();
            await RecognizeSpeechAsync();
        }
    }
}
