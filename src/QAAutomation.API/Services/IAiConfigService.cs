using Microsoft.EntityFrameworkCore;
using QAAutomation.API.Data;
using QAAutomation.API.DTOs;
using QAAutomation.API.Models;

namespace QAAutomation.API.Services;

/// <summary>Reads and writes the AI configuration stored in the database.</summary>
public interface IAiConfigService
{
    Task<AiConfig> GetAsync();
    Task<AiConfig> SaveAsync(AiConfigDto dto);
}

public class DbAiConfigService : IAiConfigService
{
    private readonly AppDbContext _db;

    public DbAiConfigService(AppDbContext db) => _db = db;

    public async Task<AiConfig> GetAsync()
    {
        var cfg = await _db.AiConfigs.FirstOrDefaultAsync();
        if (cfg == null)
        {
            cfg = new AiConfig { Id = 1 };
            _db.AiConfigs.Add(cfg);
            await _db.SaveChangesAsync();
        }
        return cfg;
    }

    public async Task<AiConfig> SaveAsync(AiConfigDto dto)
    {
        var cfg = await GetAsync();
        // Null-coalesce all string assignments: ASP.NET Core model binding converts empty
        // form strings to null, so we fall back to the existing DB value (for required
        // fields) or empty string (for optional fields) rather than violating NOT NULL.
        cfg.LlmProvider = dto.LlmProvider ?? cfg.LlmProvider;
        cfg.LlmEndpoint = dto.LlmEndpoint ?? string.Empty;
        // Only update keys if a real value was submitted (not blank and not the masked placeholder "***")
        if (!string.IsNullOrEmpty(dto.LlmApiKey) && dto.LlmApiKey != "***")
            cfg.LlmApiKey = dto.LlmApiKey;
        cfg.LlmDeployment = dto.LlmDeployment ?? cfg.LlmDeployment;
        cfg.LlmTemperature = dto.LlmTemperature;
        cfg.SentimentProvider = dto.SentimentProvider ?? cfg.SentimentProvider;
        cfg.LanguageEndpoint = dto.LanguageEndpoint ?? string.Empty;
        if (!string.IsNullOrEmpty(dto.LanguageApiKey) && dto.LanguageApiKey != "***")
            cfg.LanguageApiKey = dto.LanguageApiKey;
        cfg.RagTopK = dto.RagTopK;
        cfg.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return cfg;
    }
}
