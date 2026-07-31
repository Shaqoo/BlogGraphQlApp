﻿using BlogGraphQlApp.Common;
using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.Types;
using HotChocolate.Authorization;

namespace BlogGraphQlApp.GraphQL.Queries
{
    [ExtendObjectType("Query")]
    public class ReelQueries
    {
        [Authorize]
        public async Task<ApiResponse<ReelDto?>> GetReelByIdAsync(Guid id, [Service] IReelService reelService)
            => await reelService.GetReelByIdAsync(id);

        [Authorize]
        public async Task<ApiResponse<IEnumerable<ReelDto>>> GetReelsByUserIdAsync(Guid userId, [Service] IReelService reelService)
            => await reelService.GetReelsByUserIdAsync(userId);

        [Authorize]
        [UsePaging(typeof(ReelType))]
        [GraphQLDescription("Gets a paginated feed of reels from users the current user follows.")]
        public async Task<IQueryable<ReelDto>> GetReelFeedAsync(
            [Service] IReelService reelService)
        {
            var response = await reelService.GetReelFeedAsync();
            return response.Data!; // The [UsePaging] attribute will handle the IQueryable
        }
    }
}