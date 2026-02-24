using CEMPACKSYS.Data;
using CEMPACKSYS.Models;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;

namespace CEMPACKSYS.Controllers
{

    [Route("api/[controller]")]
    [ApiController]
    public class WeightController : ControllerBase
    {
        private readonly CEMPACKSYSContext _context;
        private readonly HttpClient _httpClient;

        public WeightController(CEMPACKSYSContext context)
        {
            _context = context;
            _httpClient = new HttpClient();
        }

        [HttpGet("get")]
        public async Task<IActionResult> GetWeight(int packer, int spout)
        {
            var response = await _httpClient.GetAsync(
                $"http://localhost:5003/weight?packer={packer}&spout={spout}");

            if (!response.IsSuccessStatusCode)
                return BadRequest("Weight API failed");

            var apiData = await response.Content.ReadFromJsonAsync<WeightApiResponse>();

            if (apiData == null)
                return BadRequest("Invalid response");

            var record = new WeightRecord
            {
                PackerNo = Convert.ToInt32(apiData.packer),
                SpoutNo = Convert.ToInt32(apiData.spout),
                Weight = Math.Round(apiData.weight, 2),
                ReceivedAt = DateTime.Now
            };

            _context.WeightRecords.Add(record);
            await _context.SaveChangesAsync();

            return Ok(record);
        }

    }
}
