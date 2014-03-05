namespace PatchPayload.ServiceTools
{
	public enum ServiceControl
	{
		Stop = 1,
		Pause = 2,
		Continue = 3,
		Interrogate = 4,
		Shutdown = 5,
		ParamChange = 6,
		NetBindAdd = 7,
		NetBindRemove = 8,
		NetBindEnable = 9,
		NetBindDisable = 10
	}
}