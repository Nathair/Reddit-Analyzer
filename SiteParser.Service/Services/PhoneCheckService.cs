using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SiteParser.Service.Models;

namespace SiteParser.Service.Services
{
    public class PhoneCheckService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiUrl;

        public PhoneCheckService(HttpClient httpClient, string apiUrl)
        {
            _httpClient = httpClient;
            _apiUrl = apiUrl;
        }

        public async Task<bool> IsValidAsync(string phoneNumber)
        {
            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(new { Number = phoneNumber }), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(_apiUrl, content);

                // According to PhoneController.cs:
                // 201 Created -> Number added (valid for our purpose)
                // 409 Conflict -> Number exists (rejected)
                // 400 BadRequest -> Invalid number (maybe skip?)
                
                if (response.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    return true; // Not in DB, successfully added
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    return false; // Already in DB
                }

                return false; // Error or invalid number
            }
            catch (Exception)
            {
                // Log error
                return false;
            }
        }
    }
}
