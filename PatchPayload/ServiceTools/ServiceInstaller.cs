using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace PatchPayload.ServiceTools
{
	public class ServiceInstaller
	{
		private const int STANDARD_RIGHTS_REQUIRED = 983040;

		private const int SERVICE_WIN32_OWN_PROCESS = 16;

		public ServiceInstaller()
		{
		}

		[DllImport("advapi32.dll", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern int CloseServiceHandle(IntPtr hSCObject);

		[DllImport("advapi32.dll", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern int ControlService(IntPtr hService, ServiceControl dwControl, ServiceInstaller.SERVICE_STATUS lpServiceStatus);

		[DllImport("advapi32.dll", CharSet=CharSet.None, EntryPoint="CreateServiceA", ExactSpelling=false)]
		private static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, ServiceRights dwDesiredAccess, int dwServiceType, ServiceBootFlag dwStartType, ServiceError dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lp, string lpPassword);

		[DllImport("advapi32.dll", CharSet=CharSet.None, ExactSpelling=false, SetLastError=true)]
		private static extern int DeleteService(IntPtr hService);

		public static ServiceState GetServiceStatus(string ServiceName)
		{
			ServiceState serviceStatus;
			IntPtr intPtr = ServiceInstaller.OpenSCManager(ServiceManagerRights.Connect);
			try
			{
				IntPtr intPtr1 = ServiceInstaller.OpenService(intPtr, ServiceName, ServiceRights.QueryStatus);
				if (intPtr1 != IntPtr.Zero)
				{
					try
					{
						serviceStatus = ServiceInstaller.GetServiceStatus(intPtr1);
					}
					finally
					{
						ServiceInstaller.CloseServiceHandle(intPtr);
					}
				}
				else
				{
					serviceStatus = ServiceState.NotFound;
				}
			}
			finally
			{
				ServiceInstaller.CloseServiceHandle(intPtr);
			}
			return serviceStatus;
		}

		private static ServiceState GetServiceStatus(IntPtr hService)
		{
			ServiceInstaller.SERVICE_STATUS sERVICESTATU = new ServiceInstaller.SERVICE_STATUS();
			if (ServiceInstaller.QueryServiceStatus(hService, sERVICESTATU) == 0)
			{
				throw new ApplicationException("Failed to query service status.");
			}
			return sERVICESTATU.dwCurrentState;
		}

		public static void InstallAndStart(string ServiceName, string DisplayName, string FileName)
		{
			IntPtr intPtr = ServiceInstaller.OpenSCManager(ServiceManagerRights.Connect | ServiceManagerRights.CreateService);
			try
			{
				IntPtr intPtr1 = ServiceInstaller.OpenService(intPtr, ServiceName, ServiceRights.QueryStatus | ServiceRights.Start);
				if (intPtr1 == IntPtr.Zero)
				{
					intPtr1 = ServiceInstaller.CreateService(intPtr, ServiceName, DisplayName, ServiceRights.QueryStatus | ServiceRights.Start, 16, ServiceBootFlag.AutoStart, ServiceError.Normal, FileName, null, IntPtr.Zero, null, null, null);
				}
				if (intPtr1 == IntPtr.Zero)
				{
					throw new ApplicationException("Failed to install service.");
				}
				try
				{
					ServiceInstaller.StartService(intPtr1);
				}
				finally
				{
					ServiceInstaller.CloseServiceHandle(intPtr1);
				}
			}
			finally
			{
				ServiceInstaller.CloseServiceHandle(intPtr);
			}
		}

		[DllImport("advapi32.dll", CharSet=CharSet.None, EntryPoint="OpenSCManagerA", ExactSpelling=false)]
		private static extern IntPtr OpenSCManager(string lpMachineName, string lpDatabaseName, ServiceManagerRights dwDesiredAccess);

		private static IntPtr OpenSCManager(ServiceManagerRights Rights)
		{
			IntPtr intPtr = ServiceInstaller.OpenSCManager(null, null, Rights);
			if (intPtr == IntPtr.Zero)
			{
				throw new ApplicationException("Could not connect to service control manager.");
			}
			return intPtr;
		}

		[DllImport("advapi32.dll", CharSet=CharSet.Ansi, EntryPoint="OpenServiceA", ExactSpelling=false)]
		private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, ServiceRights dwDesiredAccess);

		[DllImport("advapi32.dll", CharSet=CharSet.None, ExactSpelling=false)]
		private static extern int QueryServiceStatus(IntPtr hService, ServiceInstaller.SERVICE_STATUS lpServiceStatus);

		public static bool ServiceIsInstalled(string ServiceName)
		{
			bool flag;
			IntPtr intPtr = ServiceInstaller.OpenSCManager(ServiceManagerRights.Connect);
			try
			{
				IntPtr intPtr1 = ServiceInstaller.OpenService(intPtr, ServiceName, ServiceRights.QueryStatus);
				if (intPtr1 != IntPtr.Zero)
				{
					ServiceInstaller.CloseServiceHandle(intPtr1);
					flag = true;
				}
				else
				{
					flag = false;
				}
			}
			finally
			{
				ServiceInstaller.CloseServiceHandle(intPtr);
			}
			return flag;
		}

		[DllImport("advapi32.dll", CharSet=CharSet.None, EntryPoint="StartServiceA", ExactSpelling=false)]
		private static extern int StartService(IntPtr hService, int dwNumServiceArgs, int lpServiceArgVectors);

		public static void StartService(string Name)
		{
			IntPtr intPtr = ServiceInstaller.OpenSCManager(ServiceManagerRights.Connect);
			try
			{
				IntPtr intPtr1 = ServiceInstaller.OpenService(intPtr, Name, ServiceRights.QueryStatus | ServiceRights.Start);
				if (intPtr1 == IntPtr.Zero)
				{
					throw new ApplicationException("Could not open service.");
				}
				try
				{
					ServiceInstaller.StartService(intPtr1);
				}
				finally
				{
					ServiceInstaller.CloseServiceHandle(intPtr1);
				}
			}
			finally
			{
				ServiceInstaller.CloseServiceHandle(intPtr);
			}
		}

		private static void StartService(IntPtr hService)
		{
			ServiceInstaller.SERVICE_STATUS sERVICESTATU = new ServiceInstaller.SERVICE_STATUS();
			ServiceInstaller.StartService(hService, 0, 0);
			ServiceInstaller.WaitForServiceStatus(hService, ServiceState.Starting, ServiceState.Run);
		}

		public static void StopService(string Name)
		{
			IntPtr intPtr = ServiceInstaller.OpenSCManager(ServiceManagerRights.Connect);
			try
			{
				IntPtr intPtr1 = ServiceInstaller.OpenService(intPtr, Name, ServiceRights.QueryStatus | ServiceRights.Stop);
				if (intPtr1 == IntPtr.Zero)
				{
					throw new ApplicationException("Could not open service.");
				}
				try
				{
					ServiceInstaller.StopService(intPtr1);
				}
				finally
				{
					ServiceInstaller.CloseServiceHandle(intPtr1);
				}
			}
			finally
			{
				ServiceInstaller.CloseServiceHandle(intPtr);
			}
		}

		private static void StopService(IntPtr hService)
		{
			ServiceInstaller.ControlService(hService, ServiceControl.Stop, new ServiceInstaller.SERVICE_STATUS());
			ServiceInstaller.WaitForServiceStatus(hService, ServiceState.Stopping, ServiceState.Stop);
		}

		public static void Uninstall(string ServiceName)
		{
			IntPtr intPtr = ServiceInstaller.OpenSCManager(ServiceManagerRights.Connect);
			try
			{
				IntPtr intPtr1 = ServiceInstaller.OpenService(intPtr, ServiceName, ServiceRights.QueryStatus | ServiceRights.Stop | ServiceRights.Delete | ServiceRights.StandardRightsRequired);
				if (intPtr1 == IntPtr.Zero)
				{
					throw new ApplicationException("Service not installed.");
				}
				try
				{
					ServiceInstaller.StopService(intPtr1);
					if (ServiceInstaller.DeleteService(intPtr1) == 0)
					{
						int lastWin32Error = Marshal.GetLastWin32Error();
						throw new ApplicationException(string.Concat("Could not delete service ", lastWin32Error));
					}
				}
				finally
				{
					ServiceInstaller.CloseServiceHandle(intPtr1);
				}
			}
			finally
			{
				ServiceInstaller.CloseServiceHandle(intPtr);
			}
		}

		private static bool WaitForServiceStatus(IntPtr hService, ServiceState WaitStatus, ServiceState DesiredStatus)
		{
			ServiceInstaller.SERVICE_STATUS sERVICESTATU = new ServiceInstaller.SERVICE_STATUS();
			ServiceInstaller.QueryServiceStatus(hService, sERVICESTATU);
			if (sERVICESTATU.dwCurrentState == DesiredStatus)
			{
				return true;
			}
			int tickCount = Environment.TickCount;
			int num = sERVICESTATU.dwCheckPoint;
			do
			{
			Label0:
				if (sERVICESTATU.dwCurrentState != WaitStatus)
				{
					break;
				}
				int num1 = sERVICESTATU.dwWaitHint / 10;
				if (num1 < 1000)
				{
					num1 = 1000;
				}
				else if (num1 > 10000)
				{
					num1 = 10000;
				}
				Thread.Sleep(num1);
				if (ServiceInstaller.QueryServiceStatus(hService, sERVICESTATU) == 0)
				{
					break;
				}
				if (sERVICESTATU.dwCheckPoint <= num)
				{
					continue;
				}
				tickCount = Environment.TickCount;
				num = sERVICESTATU.dwCheckPoint;
				goto Label0;
			}
			while (Environment.TickCount - tickCount <= sERVICESTATU.dwWaitHint);
			return sERVICESTATU.dwCurrentState == DesiredStatus;
		}

		private class SERVICE_STATUS
		{
			public int dwServiceType;

			public ServiceState dwCurrentState;

			public int dwControlsAccepted;

			public int dwWin32ExitCode;

			public int dwServiceSpecificExitCode;

			public int dwCheckPoint;

			public int dwWaitHint;

			public SERVICE_STATUS()
			{
			}
		}
	}
}