local CorePackages = game:GetService("CorePackages")
local PolicyProvider = require(CorePackages.Packages.PolicyProvider)

local PolicyImplementation = PolicyProvider.GetPolicyImplementations.MemStorageService("app-policy")
local SelfieViewPolicy = PolicyProvider.withGetPolicyImplementation(PolicyImplementation)

SelfieViewPolicy.Mapper = function(policy)
	return {
		eligibleForSelfieViewFeature = function()
			return policy.EligibleForSelfieViewFeature or false
		end,
	}
end

SelfieViewPolicy.PolicyImplementation = PolicyImplementation

return SelfieViewPolicy
