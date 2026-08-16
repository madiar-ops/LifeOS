using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Career;

namespace LifeOS.Application.Interfaces.Services;

public interface ICareerService
{
    Task<CareerProfileResponse> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<CareerProfileResponse> UpdateProfileAsync(
        UpdateCareerProfileRequest request, CancellationToken cancellationToken = default);

    Task<AiResultResponse<ResumeAnalysisResponse>> AnalyzeResumeAsync(
        CancellationToken cancellationToken = default);
}
