namespace PatchPayload.ServiceTools
{
	public enum ServiceState
	{
		Unknown = -1,
		NotFound = 0,
		Stop = 1,
		Run = 2,
		Stopping = 3,
		Starting = 4
	}
}