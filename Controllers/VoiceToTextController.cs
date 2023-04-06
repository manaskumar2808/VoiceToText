using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace VoiceToTextConversion {
    [ApiController]
    [Route("api/voice-to-text/")]
    public class VoiceToTextController : ControllerBase
    {
        private readonly string ApiToken = "28a373d5f64d4e5681505301ed4ef2d2";
        private readonly HttpClient httpClient = new HttpClient();

        [HttpGet]
        [ActionName("")]
        public ActionResult Index()
        {
            return Ok("Welcome to Voice/Text transcriber!");
        }

        [HttpPost]
        public async Task<ActionResult<string>> Transcribe(IFormFile file) {
            try {
                if(file == null)
                    return BadRequest("File is missing.");
                
                var uploadUrl = await UploadFile(file);
                var transcriptionResult = await SubmitForTranscription(uploadUrl);
                return transcriptionResult;
            } catch(Exception ex) {
                return BadRequest("An error occurred while processing the request. Error message: " + ex.Message);
            }
        }

        private async Task<string> UploadFile(IFormFile file) {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("authorization", ApiToken);

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.assemblyai.com/v2/upload");
            request.Headers.Add("Transfer-Encoding", "chunked");

            var fileStream = file.OpenReadStream();
            var streamContent = new StreamContent(fileStream);
            request.Content = streamContent;

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var deserializedResponseJson = JsonConvert.DeserializeObject(responseJson); 
            if(deserializedResponseJson == null)
            throw new Exception("Request failed.");

            dynamic responseObject = deserializedResponseJson;

            return responseObject.upload_url; 
        }

        private async Task<string> SubmitForTranscription(string uploadUrl) {
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("authorization", ApiToken);

            var json = new {
                audio_url = uploadUrl
            };

            var payload = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://api.assemblyai.com/v2/transcript", payload);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            var deserializedResponseJson = JsonConvert.DeserializeObject(responseJson); 
            if(deserializedResponseJson == null)
                throw new Exception("Request failed.");

            dynamic responseObject = deserializedResponseJson;

            var transcriptId = responseObject.id;
            var transcriptUrl = $"https://api.assemblyai.com/v2/transcript/{transcriptId}";

            var status = "";

            while(status != "completed") {
                await Task.Delay(TimeSpan.FromSeconds(1));
                response = await httpClient.GetAsync(transcriptUrl);
                response.EnsureSuccessStatusCode();
                responseJson = await response.Content.ReadAsStringAsync();
                deserializedResponseJson = JsonConvert.DeserializeObject(responseJson); 
                if(deserializedResponseJson == null)
                    throw new Exception("Request failed.");
                responseObject = deserializedResponseJson;
                status = responseObject.status;
            }

            return responseObject.text;
        }
    }
}