using JobNexus.Core.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;

namespace JobNexus.Services;

public class CurrentUserService
{
    private readonly UserManager<Profile> _userManager;
    private readonly AuthenticationStateProvider _authProvider;
    private Profile? _cachedProfile;
    
    public CurrentUserService(UserManager<Profile> userManager,
        AuthenticationStateProvider authProvider)
    {
        _userManager = userManager;
        _authProvider = authProvider;
    }

    public async Task<Profile?> GetProfileAsync()
    {
        if (_cachedProfile != null)
            return _cachedProfile;

        var authState = await _authProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated == true)
            _cachedProfile = await _userManager.GetUserAsync(user);

        return _cachedProfile;
    }

    public void ClearCache() => _cachedProfile = null;
}
