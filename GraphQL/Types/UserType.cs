using BlogGraphQlApp.Core.Interfaces;
using BlogGraphQlApp.DTOs;
using BlogGraphQlApp.GraphQL.DataLoaders;
using BlogGraphQlApp.GraphQL.Resolvers;
using System.Diagnostics;

namespace BlogGraphQlApp.GraphQL.Types
{
    public class UserType : ObjectType<UserDto>
    {
        protected override void Configure(IObjectTypeDescriptor<UserDto> descriptor)
        {
            descriptor.Field(u => u.Id).Type<NonNullType<IdType>>();

            descriptor
                .Field("followersCount")
                .Type<IntType>()
                .Resolve(async (ctx, ct) =>
                {
                    var user = ctx.Parent<UserDto>();
                    var dataLoader = ctx.Service<FollowersByUserIdDataLoader>();
                    return await dataLoader.LoadAsync(user.Id, ct);
                });

            descriptor
                .Field("followingCount")
                .Type<IntType>()
                .Resolve(async (ctx, ct) =>
                {
                    var user = ctx.Parent<UserDto>();
                    var dataLoader = ctx.Service<FollowingByUserIdDataLoader>();
                    return await dataLoader.LoadAsync(user.Id, ct);
                });

            descriptor
                .Field("followers")
                .UsePaging<UserType>()
                .Resolve(async ctx =>
                {
                    var user = ctx.Parent<UserDto>();
                    var service = ctx.Service<IUserFollowService>();
                    var response = await service.GetFollowersAsync(user.Id);
                    return response.Data!;
                });

            descriptor
                .Field("following")
                .UsePaging<UserType>()
                .Resolve(async ctx =>
                {
                    var user = ctx.Parent<UserDto>();
                    var service = ctx.Service<IUserFollowService>();
                    return await service.GetFollowingAsync(user.Id);
                });

            descriptor
                .Field("isFollowedByCurrentUser")
                .Type<BooleanType>()
                .Resolve(async ctx =>
                {
                    var user = ctx.Parent<UserDto>();
                    var authService = ctx.Service<IAuthService>();
                    var userFollowService = ctx.Service<IUserFollowService>();
                    var currentUser = await authService.GetCurrentUserAsync();
                    if (currentUser.Data == null)
                    {
                        return false;
                    }
                    try
                    {
                        var res = await userFollowService.IsUserFollowedByAsync(currentUser.Data.Id, user.Id);
                        return res;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error checking follow status: {Message}", ex.Message);
                        return false;
                    }
                });

            descriptor
                .Field("isOnline")
                .Description("Field that detects whether a user is online or not")
                .Type<BooleanType>()
                .ResolveWith<UserResolvers>(a => a.CheckIfIsOnline(default!, default!));
        }
    }
}