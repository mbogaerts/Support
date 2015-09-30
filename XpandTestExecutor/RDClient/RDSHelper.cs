using System;
using System.Runtime.InteropServices;

namespace RDClient {
    class RDSHelper {
        public const int WTSCurrentServerHandle = 0;
        [DllImport("Wtsapi32.dll")]
        private static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WtsInfoClass wtsInfoClass,
            out IntPtr ppBuffer, out int pBytesReturned);

        public enum WtsInfoClass {
            WTSInitialProgram,


            WTSApplicationName,


            WTSWorkingDirectory,


            WTSOEMId,


            WTSSessionId,


            WTSUserName,


            WTSWinStationName,

            WTSDomainName,


            WTSConnectState,

            WTSClientBuildNumber,


            WTSClientName,


            WTSClientDirectory,


            WTSClientProductId,


            WTSClientHardwareId,


            WTSClientAddress,
            WTSClientDisplay,
            WTSClientProtocolType,

            WTSIdleTime,

            WTSLogonTime,
            WTSIncomingBytes,
            WTSOutgoingBytes,
            WTSIncomingFrames,
            WTSOutgoingFrames,
            WTSClientInfo,
            WTSSessionInfo
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WTSSessionInfo {
            public Int32 SessionID;
            public string pWinStationName;
            public WTSConnectstateClass State;
        }

        public enum WTSConnectstateClass {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }

        public struct SessionInfo {
            private readonly IntPtr _intPtr;
            private readonly WTSSessionInfo? _wtsSessionInfo;

            public SessionInfo(WTSSessionInfo? wtsSessionInfo, IntPtr intPtr)
                : this() {
                _wtsSessionInfo = wtsSessionInfo;
                _intPtr = intPtr;
            }

            public IntPtr IntPtr {
                get { return _intPtr; }
            }

            public WTSSessionInfo? Info {
                get { return _wtsSessionInfo; }
            }
        }

        [DllImport("WTSAPI32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool WTSEnumerateSessions(
            IntPtr hServer,
            [MarshalAs(UnmanagedType.U4)] UInt32 reserved,
            [MarshalAs(UnmanagedType.U4)] UInt32 version,
            ref IntPtr ppSessionInfo,
            [MarshalAs(UnmanagedType.U4)] ref UInt32 pSessionInfoCount
            );

        public static WTSSessionInfo? GetWTSSessionInfo(string userName, uint sessionCount, IntPtr ppSessionInfo) {
            for (int i = 0; i < sessionCount; i++) {
                WTSSessionInfo? wtsSessionInfo = (WTSSessionInfo)Marshal.PtrToStructure(
                    ppSessionInfo + i * Marshal.SizeOf(typeof(WTSSessionInfo)),
                    typeof(WTSSessionInfo));
                if (wtsSessionInfo.Value.State == WTSConnectstateClass.WTSActive &&
                    GetUsernameBySessionId(wtsSessionInfo.Value.SessionID, false).ToLower() == userName.ToLower())
                    return wtsSessionInfo;
            }
            return null;
        }

        [DllImport("WTSAPI32.DLL", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        public static string GetUsernameBySessionId(int sessionId, bool prependDomain) {
            IntPtr buffer;
            int strLen;
            string username = "SYSTEM";
            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WtsInfoClass.WTSUserName, out buffer, out strLen) &&
                strLen > 1) {
                username = Marshal.PtrToStringAnsi(buffer);
                WTSFreeMemory(buffer);
                if (prependDomain) {
                    if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WtsInfoClass.WTSDomainName, out buffer,
                            out strLen) && strLen > 1) {
                        username = Marshal.PtrToStringAnsi(buffer) + "\\" + username;
                        WTSFreeMemory(buffer);
                    }
                }
            }
            return username;
        }

        public static SessionInfo GetSessionInfo(string userName) {
            IntPtr ppSessionInfo = IntPtr.Zero;
            UInt32 sessionCount = 0;
            bool wtsEnumerateSessions = WTSEnumerateSessions((IntPtr)WTSCurrentServerHandle, 0, 1, ref ppSessionInfo,
                ref sessionCount);
            if (wtsEnumerateSessions) {
                WTSSessionInfo? wtsSessionInfo = GetWTSSessionInfo(userName, sessionCount, ppSessionInfo);
                return wtsSessionInfo == null
                    ? new SessionInfo(null, ppSessionInfo)
                    : new SessionInfo(wtsSessionInfo, ppSessionInfo);
            }
            return new SessionInfo(null, ppSessionInfo);
        }

        public static bool SessionExists(string userName) {
            SessionInfo sessionInfo = GetSessionInfo(userName);
            WTSFreeMemory(sessionInfo.IntPtr);
            return sessionInfo.Info != null;
        }

        public static string GetSessionId(string userName){
            var sessionInfo = GetSessionInfo(userName).Info;
            return sessionInfo != null ? sessionInfo.Value.SessionID.ToString() : null;
        }
    }
}
