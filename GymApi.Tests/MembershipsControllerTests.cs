using System.Security.Claims;
using GymApi.Controllers;
using GymApi.Data;
using GymApi.DTOs;
using GymApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GymApi.Tests;

public class MembershipsControllerTests
{
    private static GymDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GymDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GymDbContext(options);
    }

    private static void SetUserContext(ControllerBase controller, Guid userId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    private static async Task<(User member, MembershipPlan plan)> SeedMemberAndPlan(GymDbContext db)
    {
        var member = new User
        {
            UserId = Guid.NewGuid(),
            Email = "member@example.com",
            Role = "Member"
        };
        var plan = new MembershipPlan
        {
            MembershipPlanId = Guid.NewGuid(),
            Name = "Premium",
            Price = 1999m,
            DurationInDays = 90,
            Benefits = "Unlimited group classes and one trainer consultation.",
            IsActive = true
        };

        db.Users.Add(member);
        db.MembershipPlans.Add(plan);
        await db.SaveChangesAsync();

        return (member, plan);
    }

    [Fact]
    public async Task CreatePlan_AsAdmin_ReturnsCreated()
    {
        using var db = CreateDbContext();
        var controller = new MembershipPlansController(db);
        SetUserContext(controller, Guid.NewGuid(), "Admin");

        var result = await controller.CreatePlan(new CreateMembershipPlanRequest
        {
            Name = "Basic",
            Price = 999m,
            DurationInDays = 30,
            Benefits = "Gym access during standard hours."
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var plan = Assert.IsType<MembershipPlanResponse>(created.Value);
        Assert.Equal("Basic", plan.Name);
        Assert.Equal("Gym access during standard hours.", plan.Benefits);
    }

    [Fact]
    public async Task RequestMembership_ForActivePlan_ReturnsPendingRequest()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        var controller = new MembershipsController(db);
        SetUserContext(controller, member.UserId, "Member");

        var result = await controller.RequestMembership(new CreateMembershipRequest
        {
            MembershipPlanId = plan.MembershipPlanId
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<MembershipResponse>(created.Value);
        Assert.Equal(MembershipViewStatus.Pending.ToString(), response.Status);
        Assert.Null(response.StartDate);
        Assert.Null(response.EndDate);
    }

    [Fact]
    public async Task RequestMembership_WhenPendingRequestExists_ReturnsConflict()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        db.Memberships.Add(new Membership
        {
            MembershipId = Guid.NewGuid(),
            UserId = member.UserId,
            MembershipPlanId = plan.MembershipPlanId,
            RequestedAt = DateTime.UtcNow,
            Status = MembershipStatus.Pending
        });
        await db.SaveChangesAsync();

        var controller = new MembershipsController(db);
        SetUserContext(controller, member.UserId, "Member");

        var result = await controller.RequestMembership(new CreateMembershipRequest
        {
            MembershipPlanId = plan.MembershipPlanId
        });

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task RequestMembership_ForInactivePlan_ReturnsNotFound()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        plan.IsActive = false;
        await db.SaveChangesAsync();

        var controller = new MembershipsController(db);
        SetUserContext(controller, member.UserId, "Member");

        var result = await controller.RequestMembership(new CreateMembershipRequest
        {
            MembershipPlanId = plan.MembershipPlanId
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ReviewMembership_ApprovalActivatesMembershipForPlanDuration()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        var membership = new Membership
        {
            MembershipId = Guid.NewGuid(),
            UserId = member.UserId,
            MembershipPlanId = plan.MembershipPlanId,
            RequestedAt = DateTime.UtcNow,
            Status = MembershipStatus.Pending
        };
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();

        var controller = new MembershipsController(db);
        SetUserContext(controller, Guid.NewGuid(), "Admin");

        var result = await controller.ReviewMembership(membership.MembershipId, new ReviewMembershipRequest
        {
            Approve = true,
            ReviewNote = "Approved after profile verification."
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<MembershipResponse>(ok.Value);
        Assert.Equal(MembershipViewStatus.Active.ToString(), response.Status);
        Assert.NotNull(response.StartDate);
        Assert.Equal(response.StartDate!.Value.AddDays(plan.DurationInDays), response.EndDate);
    }

    [Fact]
    public async Task ReviewMembership_RejectionKeepsMembershipInactive()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        var membership = new Membership
        {
            MembershipId = Guid.NewGuid(),
            UserId = member.UserId,
            MembershipPlanId = plan.MembershipPlanId,
            RequestedAt = DateTime.UtcNow,
            Status = MembershipStatus.Pending
        };
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();

        var controller = new MembershipsController(db);
        SetUserContext(controller, Guid.NewGuid(), "Admin");

        var result = await controller.ReviewMembership(membership.MembershipId, new ReviewMembershipRequest
        {
            Approve = false,
            ReviewNote = "Please contact reception with an identity document."
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<MembershipResponse>(ok.Value);
        Assert.Equal(MembershipViewStatus.Rejected.ToString(), response.Status);
        Assert.Null(response.StartDate);
    }

    [Fact]
    public async Task GetMyMemberships_ReturnsOnlyCurrentMembersRequests()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        var otherMember = new User
        {
            UserId = Guid.NewGuid(),
            Email = "other-member@example.com",
            Role = "Member"
        };
        db.Users.Add(otherMember);
        db.Memberships.AddRange(
            new Membership
            {
                MembershipId = Guid.NewGuid(),
                UserId = member.UserId,
                MembershipPlanId = plan.MembershipPlanId,
                Status = MembershipStatus.Pending
            },
            new Membership
            {
                MembershipId = Guid.NewGuid(),
                UserId = otherMember.UserId,
                MembershipPlanId = plan.MembershipPlanId,
                Status = MembershipStatus.Pending
            });
        await db.SaveChangesAsync();

        var controller = new MembershipsController(db);
        SetUserContext(controller, member.UserId, "Member");

        var result = await controller.GetMyMemberships();

        var ok = Assert.IsType<OkObjectResult>(result);
        var memberships = Assert.IsAssignableFrom<IEnumerable<MembershipResponse>>(ok.Value).ToList();
        Assert.Single(memberships);
        Assert.Equal(member.UserId, memberships[0].UserId);
    }

    [Fact]
    public async Task ReviewMembership_AlreadyReviewedRequest_ReturnsBadRequest()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        var membership = new Membership
        {
            MembershipId = Guid.NewGuid(),
            UserId = member.UserId,
            MembershipPlanId = plan.MembershipPlanId,
            Status = MembershipStatus.Rejected,
            ReviewedAt = DateTime.UtcNow
        };
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();

        var controller = new MembershipsController(db);
        SetUserContext(controller, Guid.NewGuid(), "Admin");

        var result = await controller.ReviewMembership(membership.MembershipId, new ReviewMembershipRequest
        {
            Approve = true
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(MembershipStatus.Rejected, membership.Status);
    }

    [Fact]
    public async Task ReviewMembership_WhenMemberHasActiveMembership_ReturnsConflict()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        var activeMembership = new Membership
        {
            MembershipId = Guid.NewGuid(),
            UserId = member.UserId,
            MembershipPlanId = plan.MembershipPlanId,
            Status = MembershipStatus.Approved,
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.AddDays(30)
        };
        var pendingMembership = new Membership
        {
            MembershipId = Guid.NewGuid(),
            UserId = member.UserId,
            MembershipPlanId = plan.MembershipPlanId,
            Status = MembershipStatus.Pending
        };
        db.Memberships.AddRange(activeMembership, pendingMembership);
        await db.SaveChangesAsync();

        var controller = new MembershipsController(db);
        SetUserContext(controller, Guid.NewGuid(), "Admin");

        var result = await controller.ReviewMembership(pendingMembership.MembershipId, new ReviewMembershipRequest
        {
            Approve = true
        });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(MembershipStatus.Pending, pendingMembership.Status);
    }

    [Fact]
    public async Task ExpiredMembership_IsShownAsExpiredAndAllowsNewRequest()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        db.Memberships.Add(new Membership
        {
            MembershipId = Guid.NewGuid(),
            UserId = member.UserId,
            MembershipPlanId = plan.MembershipPlanId,
            Status = MembershipStatus.Approved,
            StartDate = DateTime.UtcNow.AddDays(-31),
            EndDate = DateTime.UtcNow.AddDays(-1),
            PlanName = plan.Name,
            PlanPrice = plan.Price,
            PlanDurationInDays = plan.DurationInDays,
            PlanBenefits = plan.Benefits
        });
        await db.SaveChangesAsync();

        var controller = new MembershipsController(db);
        SetUserContext(controller, member.UserId, "Member");

        var historyResult = await controller.GetMyMemberships();
        var history = Assert.IsAssignableFrom<IEnumerable<MembershipResponse>>(
            Assert.IsType<OkObjectResult>(historyResult).Value).ToList();
        Assert.Equal(MembershipViewStatus.Expired.ToString(), history.Single().Status);

        var requestResult = await controller.RequestMembership(new CreateMembershipRequest
        {
            MembershipPlanId = plan.MembershipPlanId
        });

        Assert.IsType<CreatedAtActionResult>(requestResult);
    }

    [Fact]
    public async Task ApprovedMembership_UsesPlanTermsFromApprovalDate()
    {
        using var db = CreateDbContext();
        var (member, plan) = await SeedMemberAndPlan(db);
        var membership = new Membership
        {
            MembershipId = Guid.NewGuid(),
            UserId = member.UserId,
            MembershipPlanId = plan.MembershipPlanId,
            Status = MembershipStatus.Pending
        };
        db.Memberships.Add(membership);
        await db.SaveChangesAsync();

        var adminController = new MembershipsController(db);
        SetUserContext(adminController, Guid.NewGuid(), "Admin");
        await adminController.ReviewMembership(membership.MembershipId, new ReviewMembershipRequest { Approve = true });

        plan.Name = "Premium Plus";
        plan.Price = 2999m;
        plan.DurationInDays = 180;
        plan.Benefits = "Updated benefits";
        await db.SaveChangesAsync();

        var memberController = new MembershipsController(db);
        SetUserContext(memberController, member.UserId, "Member");
        var result = await memberController.GetMyMemberships();
        var response = Assert.IsAssignableFrom<IEnumerable<MembershipResponse>>(
            Assert.IsType<OkObjectResult>(result).Value).Single();

        Assert.Equal("Premium", response.PlanName);
        Assert.Equal(1999m, response.Price);
        Assert.Equal(90, response.DurationInDays);
        Assert.Equal("Unlimited group classes and one trainer consultation.", response.Benefits);
    }

    [Fact]
    public void MembershipEndpoints_HaveExpectedRoleRestrictions()
    {
        var requestAttribute = typeof(MembershipsController)
            .GetMethod(nameof(MembershipsController.RequestMembership))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();
        var reviewAttribute = typeof(MembershipsController)
            .GetMethod(nameof(MembershipsController.ReviewMembership))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("Member", requestAttribute.Roles);
        Assert.Equal("Admin", reviewAttribute.Roles);
    }
}
