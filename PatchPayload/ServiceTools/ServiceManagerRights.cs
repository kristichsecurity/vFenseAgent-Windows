using System;

namespace PatchPayload.ServiceTools
{
	[Flags]
	public enum ServiceManagerRights
	{
		Connect = 1,
		CreateService = 2,
		EnumerateService = 4,
		Lock = 8,
		QueryLockStatus = 16,
		ModifyBootConfig = 32,
		StandardRightsRequired = 983040,
		AllAccess = 983103
	}
}