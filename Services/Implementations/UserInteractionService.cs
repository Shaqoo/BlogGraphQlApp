using AutoMapper;
using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.Models;
using BlogGraphQlApp.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlogGraphQlApp.Infrastructure.Services
{
    public class UserInteractionService : IUserInteractionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<UserInteractionService> _logger;

        public UserInteractionService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<UserInteractionService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<UserInteractionDto>> LogOrUpdateInteractionAsync(CreateUserInteractionDto createDto)
        {
            _logger.LogInformation("Logging or updating interaction for user {UserId} on PostId: {PostId}, ReelId: {ReelId}", createDto.UserId, createDto.PostId, createDto.ReelId);

            var existingInteraction = await _unitOfWork.UserInteractions
                .Find(i => i.UserId == createDto.UserId && (i.PostId == createDto.PostId || i.ReelId == createDto.ReelId))
                .FirstOrDefaultAsync();

            if (existingInteraction != null)
            {
                _logger.LogInformation("Existing interaction {InteractionId} found. Updating time spent.", existingInteraction.Id);
                existingInteraction.TimeSpentInSeconds += createDto.TimeSpentInSeconds;
                if (createDto.IsFavorite)
                {
                    existingInteraction.IsFavorite = true;
                }
                _unitOfWork.UserInteractions.Update(existingInteraction);
                await _unitOfWork.CompleteAsync();
                return ApiResponse<UserInteractionDto>.Success(_mapper.Map<UserInteractionDto>(existingInteraction), "Interaction updated successfully.");
            }
            else
            {
                _logger.LogInformation("No existing interaction found. Creating a new one.");
                var newInteraction = _mapper.Map<UserInteraction>(createDto);
                await _unitOfWork.UserInteractions.AddAsync(newInteraction);
                await _unitOfWork.CompleteAsync();
                return ApiResponse<UserInteractionDto>.Success(_mapper.Map<UserInteractionDto>(newInteraction), "Interaction logged successfully.");
            }
        }

        public async Task<ApiResponse<UserInteractionDto>> UpdateInteractionFavoriteStatusAsync(Guid interactionId, bool isFavorite)
        {
            _logger.LogInformation("Updating favorite status for interaction {InteractionId} to {IsFavorite}", interactionId, isFavorite);

            var interaction = await _unitOfWork.UserInteractions.GetByIdAsync(interactionId);
            if (interaction == null)
            {
                _logger.LogWarning("Interaction with ID {InteractionId} not found.", interactionId);
                return ApiResponse<UserInteractionDto>.Fail("Interaction not found.");
            }

            interaction.IsFavorite = isFavorite;
            _unitOfWork.UserInteractions.Update(interaction);
            await _unitOfWork.CompleteAsync();

            _logger.LogInformation("Favorite status for interaction {InteractionId} updated successfully.", interactionId);
            return ApiResponse<UserInteractionDto>.Success(_mapper.Map<UserInteractionDto>(interaction), "Favorite status updated successfully.");
        }
    }
}