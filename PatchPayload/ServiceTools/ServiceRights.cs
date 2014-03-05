using System;

namespace PatchPayload.ServiceTools
{
	[Flags]
	public enum ServiceRights
	{
		QueryConfig = 1,
		ChangeConfig = 2,
		QueryStatus = 4,
		EnumerateDependants = 8,
		Start = 16,
		Stop = 32,
		PauseContinue = 64,
		Interrogate = 128,
		UserDefinedControl = 256,
		Delete = 65536,
		StandardRightsRequired = 983040,
		AllAccess = 983551
	}
}