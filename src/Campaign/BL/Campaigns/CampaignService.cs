using System.Security.Cryptography;
using DnDiscord.Campaign.BL.Campaigns.DTOs;
using DnDiscord.Campaign.DataAccess;
using DnDiscord.Campaign.DataAccess.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CampaignEntity = DnDiscord.Campaign.DataAccess.Models.Campaign;

namespace DnDiscord.Campaign.BL.Campaigns;

/// <summary>
/// Service for managing campaigns.
/// </summary>
public interface ICampaignService
{
    // Campaign CRUD
    Task<CampaignDetailResponse> CreateCampaignAsync(CreateCampaignRequest request, Guid userId, CancellationToken ct = default);
    Task<CampaignDetailResponse?> GetCampaignAsync(Guid campaignId, Guid userId, CancellationToken ct = default);
    Task<CampaignListResponse> ListCampaignsAsync(CampaignFilterRequest filter, Guid userId, CancellationToken ct = default);
    Task<CampaignDetailResponse?> UpdateCampaignAsync(Guid campaignId, UpdateCampaignRequest request, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteCampaignAsync(Guid campaignId, Guid userId, bool hardDelete = false, CancellationToken ct = default);
    
    // Invite codes
    Task<InviteCodeResponse?> GenerateInviteCodeAsync(Guid campaignId, GenerateInviteCodeRequest request, Guid userId, CancellationToken ct = default);
    Task<CampaignDetailResponse?> JoinCampaignAsync(JoinCampaignRequest request, Guid userId, CancellationToken ct = default);
    
    // Members
    Task<CampaignMemberListResponse> GetMembersAsync(Guid campaignId, Guid userId, CancellationToken ct = default);
    Task<CampaignMemberResponse?> AddMemberAsync(Guid campaignId, AddMemberRequest request, Guid userId, CancellationToken ct = default);
    Task<CampaignMemberResponse?> UpdateMemberAsync(Guid campaignId, Guid memberId, UpdateMemberRequest request, Guid userId, CancellationToken ct = default);
    Task<bool> RemoveMemberAsync(Guid campaignId, Guid memberId, Guid userId, CancellationToken ct = default);
    Task<bool> LeaveCampaignAsync(Guid campaignId, Guid userId, CancellationToken ct = default);

    // Campaign tree
    Task<CampaignDetailResponse?> UpdateCampaignTreeAsync(Guid campaignId, string? treeDefinition, Guid userId, CancellationToken ct = default);
}

/// <summary>
/// Implementation of campaign service.
/// </summary>
public class CampaignService : ICampaignService
{
    private readonly CampaignDbContext _dbContext;
    private readonly ICampaignValidator _validator;
    private readonly ILogger<CampaignService> _logger;
    
    public CampaignService(
        CampaignDbContext dbContext,
        ICampaignValidator validator,
        ILogger<CampaignService> logger)
    {
        _dbContext = dbContext;
        _validator = validator;
        _logger = logger;
    }
    
    #region Campaign CRUD
    
    /// <inheritdoc />
    public async Task<CampaignDetailResponse> CreateCampaignAsync(
        CreateCampaignRequest request, 
        Guid userId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating campaign '{Name}' for user {UserId}", request.Name, userId);
        
        var validation = _validator.ValidateCreate(request);
        if (!validation.IsValid)
        {
            throw new CampaignException($"Validation failed: {string.Join(", ", validation.Errors.Select(e => e.Message))}");
        }
        
        var campaign = new CampaignEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            DungeonMasterId = userId,
            Status = request.Status,
            ImageUrl = request.ImageUrl,
            MaxPlayers = request.MaxPlayers,
            IsPublic = request.IsPublic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        _dbContext.Campaigns.Add(campaign);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created campaign {CampaignId} '{Name}'", campaign.Id, campaign.Name);

        return MapToDetailResponse(campaign, userId);
    }
    
    /// <inheritdoc />
    public async Task<CampaignDetailResponse?> GetCampaignAsync(
        Guid campaignId, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns
            .Include(c => c.Members.Where(m => m.Status == MembershipStatus.Active))
            .Include(c => c.Snapshots)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        
        if (campaign == null) return null;
        
        var isMember = campaign.Members.Any(m => m.UserId == userId);
        if (!_validator.CanView(campaign, userId, isMember))
        {
            _logger.LogWarning("User {UserId} attempted to view campaign {CampaignId} without permission", userId, campaignId);
            return null;
        }
        
        return MapToDetailResponse(campaign, userId);
    }

    /// <inheritdoc />
    public async Task<CampaignListResponse> ListCampaignsAsync(
        CampaignFilterRequest filter, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var query = _dbContext.Campaigns
            .Include(c => c.Members)
            .AsQueryable();
        
        // Apply role filter
        query = filter.RoleFilter switch
        {
            CampaignRoleFilter.AsDungeonMaster => query.Where(c => c.DungeonMasterId == userId),
            CampaignRoleFilter.AsPlayer => query.Where(c => c.Members.Any(m => m.UserId == userId && m.Status == MembershipStatus.Active)),
            _ => query.Where(c => c.IsPublic || c.DungeonMasterId == userId || c.Members.Any(m => m.UserId == userId && m.Status == MembershipStatus.Active))
        };
        
        // Apply status filter
        if (filter.Status.HasValue)
        {
            query = query.Where(c => c.Status == filter.Status.Value);
        }
        
        // Apply visibility filter
        if (filter.IsPublic.HasValue)
        {
            query = query.Where(c => c.IsPublic == filter.IsPublic.Value);
        }
        
        // Apply search
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchLower = filter.Search.ToLower();
            query = query.Where(c => 
                c.Name.ToLower().Contains(searchLower) || 
                (c.Description != null && c.Description.ToLower().Contains(searchLower)));
        }
        
        // Get total count before pagination
        var totalCount = await query.CountAsync(ct);
        
        // Apply sorting
        query = filter.SortBy switch
        {
            CampaignSortField.Name => filter.SortDescending 
                ? query.OrderByDescending(c => c.Name) 
                : query.OrderBy(c => c.Name),
            CampaignSortField.UpdatedAt => filter.SortDescending 
                ? query.OrderByDescending(c => c.UpdatedAt) 
                : query.OrderBy(c => c.UpdatedAt),
            CampaignSortField.LastPlayedAt => filter.SortDescending 
                ? query.OrderByDescending(c => c.LastPlayedAt) 
                : query.OrderBy(c => c.LastPlayedAt),
            CampaignSortField.MemberCount => filter.SortDescending 
                ? query.OrderByDescending(c => c.Members.Count) 
                : query.OrderBy(c => c.Members.Count),
            _ => filter.SortDescending 
                ? query.OrderByDescending(c => c.CreatedAt) 
                : query.OrderBy(c => c.CreatedAt)
        };
        
        // Apply pagination
        var skip = (filter.Page - 1) * filter.PageSize;
        var campaigns = await query
            .Skip(skip)
            .Take(filter.PageSize)
            .ToListAsync(ct);
        
        return new CampaignListResponse
        {
            Items = campaigns.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize,
            HasNextPage = skip + campaigns.Count < totalCount,
            HasPreviousPage = filter.Page > 1
        };
    }
    
    /// <inheritdoc />
    public async Task<CampaignDetailResponse?> UpdateCampaignAsync(
        Guid campaignId, 
        UpdateCampaignRequest request, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        
        if (campaign == null) return null;
        
        if (!_validator.CanModify(campaign, userId))
        {
            _logger.LogWarning("User {UserId} attempted to modify campaign {CampaignId} without permission", userId, campaignId);
            throw new CampaignException("You don't have permission to modify this campaign");
        }
        
        var validation = _validator.ValidateUpdate(request);
        if (!validation.IsValid)
        {
            throw new CampaignException($"Validation failed: {string.Join(", ", validation.Errors.Select(e => e.Message))}");
        }
        
        // Update only provided fields
        if (request.Name != null) campaign.Name = request.Name;
        if (request.Description != null) campaign.Description = request.Description;
        if (request.ImageUrl != null) campaign.ImageUrl = request.ImageUrl;
        if (request.MaxPlayers.HasValue) campaign.MaxPlayers = request.MaxPlayers.Value;
        if (request.IsPublic.HasValue) campaign.IsPublic = request.IsPublic.Value;
        if (request.Status.HasValue) campaign.Status = request.Status.Value;
        
        campaign.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Updated campaign {CampaignId}", campaignId);

        return MapToDetailResponse(campaign, userId);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteCampaignAsync(
        Guid campaignId, 
        Guid userId, 
        bool hardDelete = false, 
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns
            .IgnoreQueryFilters() // Include soft-deleted for hard delete
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        
        if (campaign == null) return false;
        
        if (!_validator.CanDelete(campaign, userId))
        {
            _logger.LogWarning("User {UserId} attempted to delete campaign {CampaignId} without permission", userId, campaignId);
            throw new CampaignException("You don't have permission to delete this campaign");
        }
        
        if (hardDelete)
        {
            _dbContext.Campaigns.Remove(campaign);
            _logger.LogInformation("Hard deleted campaign {CampaignId}", campaignId);
        }
        else
        {
            campaign.IsDeleted = true;
            campaign.DeletedAt = DateTime.UtcNow;
            campaign.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Soft deleted campaign {CampaignId}", campaignId);
        }
        
        await _dbContext.SaveChangesAsync(ct);
        return true;
    }
    
    #endregion
    
    #region Invite Codes
    
    /// <inheritdoc />
    public async Task<InviteCodeResponse?> GenerateInviteCodeAsync(
        Guid campaignId, 
        GenerateInviteCodeRequest request, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns.FindAsync([campaignId], ct);
        
        if (campaign == null) return null;
        
        if (!_validator.CanModify(campaign, userId))
        {
            throw new CampaignException("You don't have permission to generate invite codes for this campaign");
        }
        
        // Generate a unique invite code
        campaign.InviteCode = GenerateUniqueCode();
        campaign.InviteCodeExpiresAt = request.ExpiresInHours.HasValue 
            ? DateTime.UtcNow.AddHours(request.ExpiresInHours.Value) 
            : null;
        campaign.UpdatedAt = DateTime.UtcNow;
        
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Generated invite code for campaign {CampaignId}", campaignId);
        
        return new InviteCodeResponse
        {
            InviteCode = campaign.InviteCode,
            ExpiresAt = campaign.InviteCodeExpiresAt,
            JoinUrl = $"/campaigns/join/{campaign.InviteCode}"
        };
    }
    
    /// <inheritdoc />
    public async Task<CampaignDetailResponse?> JoinCampaignAsync(
        JoinCampaignRequest request, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.InviteCode == request.InviteCode, ct);
        
        if (campaign == null)
        {
            throw new CampaignException("Invalid invite code");
        }
        
        // Check if code is expired
        if (campaign.InviteCodeExpiresAt.HasValue && campaign.InviteCodeExpiresAt < DateTime.UtcNow)
        {
            throw new CampaignException("Invite code has expired");
        }
        
        // Check if user is already a member
        if (campaign.Members.Any(m => m.UserId == userId && m.Status == MembershipStatus.Active))
        {
            throw new CampaignException("You are already a member of this campaign");
        }
        
        // Check if campaign is full
        var activeMembers = campaign.Members.Count(m => m.Status == MembershipStatus.Active);
        if (activeMembers >= campaign.MaxPlayers)
        {
            throw new CampaignException("Campaign is full");
        }
        
        // Check if user is the DM (DM doesn't need to join)
        if (campaign.DungeonMasterId == userId)
        {
            throw new CampaignException("You are the Dungeon Master of this campaign");
        }
        
        // Add member
        var member = new CampaignMember
        {
            Id = Guid.NewGuid(),
            CampaignId = campaign.Id,
            UserId = userId,
            Role = CampaignMemberRole.Player,
            Status = MembershipStatus.Active,
            JoinedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow
        };
        
        _dbContext.CampaignMembers.Add(member);
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("User {UserId} joined campaign {CampaignId} via invite code", userId, campaign.Id);
        
        return MapToDetailResponse(campaign, userId);
    }

    #endregion

    #region Members
    
    /// <inheritdoc />
    public async Task<CampaignMemberListResponse> GetMembersAsync(
        Guid campaignId, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        
        if (campaign == null)
        {
            return new CampaignMemberListResponse();
        }
        
        var isMember = campaign.Members.Any(m => m.UserId == userId);
        if (!_validator.CanView(campaign, userId, isMember))
        {
            return new CampaignMemberListResponse();
        }
        
        var members = campaign.Members
            .Where(m => m.Status == MembershipStatus.Active || m.Status == MembershipStatus.Pending)
            .Select(MapToMemberResponse)
            .ToList();
        
        return new CampaignMemberListResponse
        {
            Items = members,
            TotalCount = members.Count
        };
    }
    
    /// <inheritdoc />
    public async Task<CampaignMemberResponse?> AddMemberAsync(
        Guid campaignId, 
        AddMemberRequest request, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);
        
        if (campaign == null) return null;
        
        if (!_validator.CanManageMembers(campaign, userId))
        {
            throw new CampaignException("You don't have permission to add members to this campaign");
        }
        
        // Check if user is already a member
        if (campaign.Members.Any(m => m.UserId == request.UserId && m.Status == MembershipStatus.Active))
        {
            throw new CampaignException("User is already a member of this campaign");
        }
        
        var member = new CampaignMember
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            UserId = request.UserId,
            Role = request.Role,
            Status = MembershipStatus.Pending,
            JoinedAt = DateTime.UtcNow
        };
        
        _dbContext.CampaignMembers.Add(member);
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Added member {MemberId} to campaign {CampaignId}", request.UserId, campaignId);
        
        return MapToMemberResponse(member);
    }
    
    /// <inheritdoc />
    public async Task<CampaignMemberResponse?> UpdateMemberAsync(
        Guid campaignId, 
        Guid memberId, 
        UpdateMemberRequest request, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns.FindAsync([campaignId], ct);
        if (campaign == null) return null;
        
        var member = await _dbContext.CampaignMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.CampaignId == campaignId, ct);
        
        if (member == null) return null;
        
        // Check permissions (DM can update anyone, members can update themselves)
        var canUpdate = _validator.CanManageMembers(campaign, userId) || member.UserId == userId;
        if (!canUpdate)
        {
            throw new CampaignException("You don't have permission to update this member");
        }
        
        // Only DM can change roles
        if (request.Role.HasValue && !_validator.CanManageMembers(campaign, userId))
        {
            throw new CampaignException("Only the Dungeon Master can change member roles");
        }
        
        if (request.Role.HasValue) member.Role = request.Role.Value;
        if (request.Nickname != null) member.Nickname = request.Nickname;
        if (request.Notes != null && _validator.CanManageMembers(campaign, userId)) member.Notes = request.Notes;
        
        await _dbContext.SaveChangesAsync(ct);
        
        return MapToMemberResponse(member);
    }
    
    /// <inheritdoc />
    public async Task<bool> RemoveMemberAsync(
        Guid campaignId, 
        Guid memberId, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns.FindAsync([campaignId], ct);
        if (campaign == null) return false;
        
        if (!_validator.CanManageMembers(campaign, userId))
        {
            throw new CampaignException("You don't have permission to remove members from this campaign");
        }
        
        var member = await _dbContext.CampaignMembers
            .FirstOrDefaultAsync(m => m.Id == memberId && m.CampaignId == campaignId, ct);
        
        if (member == null) return false;
        
        member.Status = MembershipStatus.Removed;
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Removed member {MemberId} from campaign {CampaignId}", memberId, campaignId);
        
        return true;
    }
    
    /// <inheritdoc />
    public async Task<bool> LeaveCampaignAsync(
        Guid campaignId, 
        Guid userId, 
        CancellationToken ct = default)
    {
        var member = await _dbContext.CampaignMembers
            .FirstOrDefaultAsync(m => m.CampaignId == campaignId && m.UserId == userId && m.Status == MembershipStatus.Active, ct);
        
        if (member == null) return false;
        
        member.Status = MembershipStatus.Left;
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("User {UserId} left campaign {CampaignId}", userId, campaignId);
        
        return true;
    }
    
    #endregion
    
    #region Campaign Tree

    /// <inheritdoc />
    public async Task<CampaignDetailResponse?> UpdateCampaignTreeAsync(
        Guid campaignId,
        string? treeDefinition,
        Guid userId,
        CancellationToken ct = default)
    {
        var campaign = await _dbContext.Campaigns
            .Include(c => c.Members)
            .Include(c => c.Snapshots)
            .FirstOrDefaultAsync(c => c.Id == campaignId, ct);

        if (campaign == null) return null;

        if (!_validator.CanModify(campaign, userId))
        {
            _logger.LogWarning("User {UserId} attempted to update tree of campaign {CampaignId} without permission", userId, campaignId);
            throw new CampaignException("You don't have permission to modify this campaign");
        }

        if (treeDefinition != null)
        {
            try { System.Text.Json.JsonDocument.Parse(treeDefinition); }
            catch (System.Text.Json.JsonException)
            { throw new CampaignException("Tree definition must be valid JSON"); }
        }

        campaign.CampaignTreeDefinition = treeDefinition;
        campaign.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Updated campaign tree for campaign {CampaignId}", campaignId);

        return MapToDetailResponse(campaign, userId);
    }

    #endregion

    #region Private Methods
    
    private static string GenerateUniqueCode()
    {
        var bytes = new byte[12]; // larger buffer to guarantee 8+ usable chars
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var code = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .ToUpperInvariant();
        return code[..8];
    }
    
    private static CampaignResponse MapToResponse(CampaignEntity campaign)
    {
        return new CampaignResponse
        {
            Id = campaign.Id,
            Name = campaign.Name,
            Description = campaign.Description,
            DungeonMasterId = campaign.DungeonMasterId,
            Status = campaign.Status,
            ImageUrl = campaign.ImageUrl,
            MaxPlayers = campaign.MaxPlayers,
            IsPublic = campaign.IsPublic,
            MemberCount = campaign.Members?.Count(m => m.Status == MembershipStatus.Active) ?? 0,
            CreatedAt = campaign.CreatedAt,
            UpdatedAt = campaign.UpdatedAt,
            LastPlayedAt = campaign.LastPlayedAt
        };
    }
    
    /// <param name="userId">The caller's user ID — used to derive <see cref="CampaignDetailResponse.IsDungeonMaster"/>
    /// at the mapping layer so every call-site is consistent and no setter can be forgotten.</param>
    private static CampaignDetailResponse MapToDetailResponse(CampaignEntity campaign, Guid userId)
    {
        return new CampaignDetailResponse
        {
            Id = campaign.Id,
            Name = campaign.Name,
            Description = campaign.Description,
            DungeonMasterId = campaign.DungeonMasterId,
            IsDungeonMaster = campaign.DungeonMasterId == userId,
            Status = campaign.Status,
            ImageUrl = campaign.ImageUrl,
            MaxPlayers = campaign.MaxPlayers,
            IsPublic = campaign.IsPublic,
            MemberCount = campaign.Members?.Count(m => m.Status == MembershipStatus.Active) ?? 0,
            CreatedAt = campaign.CreatedAt,
            UpdatedAt = campaign.UpdatedAt,
            LastPlayedAt = campaign.LastPlayedAt,
            HasInviteCode = !string.IsNullOrEmpty(campaign.InviteCode),
            InviteCodeExpiresAt = campaign.InviteCodeExpiresAt,
            Members = campaign.Members?
                .Where(m => m.Status == MembershipStatus.Active)
                .Select(MapToMemberResponse)
                .ToList() ?? [],
            SnapshotCount = campaign.Snapshots?.Count ?? 0,
            CampaignTreeDefinition = campaign.CampaignTreeDefinition
        };
    }
    
    private static CampaignMemberResponse MapToMemberResponse(CampaignMember member)
    {
        return new CampaignMemberResponse
        {
            Id = member.Id,
            UserId = member.UserId,
            Role = member.Role,
            Status = member.Status,
            Nickname = member.Nickname,
            JoinedAt = member.JoinedAt,
            AcceptedAt = member.AcceptedAt
        };
    }
    
    #endregion
}

/// <summary>
/// Exception thrown by campaign operations.
/// </summary>
public class CampaignException : Exception
{
    public CampaignException(string message) : base(message) { }
    public CampaignException(string message, Exception inner) : base(message, inner) { }
}

