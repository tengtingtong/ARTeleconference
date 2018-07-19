using UnityEngine;
using Byn.Net;
using System.Collections.Generic;
using System;
using Byn.Common;
using System.Text;

namespace Byn.Media
{
    /// <summary>
    /// UnityCallFactory allows to create new ICall objects and will dispose them
    /// automatically when unity shuts down. 
    /// 
    /// </summary>
    public class UnityCallFactory : UnitySingleton<UnityCallFactory>, ICallFactory
    {
        /// <summary>
        /// Turns on the internal low level log. This will
        /// heavily impact performance and might cause crashes. 
        /// Log appears in platform specific log output e.g.
        /// xcode on ios, logcat on android, console window for others
        /// </summary>
        private static readonly bool INTERNAL_VERBOSE_LOG = false;

        /// <summary>
        /// Can be used the activate the old video capturer using
        /// Unity's WebcamTexture
        /// Might be instable by now. Doesn't work with IL2CPP / iOS or WebGL
        /// </summary>
        private static readonly bool OBSOLETE_UNITY_CAMERA = false;


        private ICallFactory mFactory = null;
        /// <summary>
        /// Do not use. For debugging only.
        /// </summary>
        public ICallFactory InternalFactory
        {
            get
            {
                return mFactory;
            }
        }
        private bool mIsDisposed = false;

#if !UNITY_WEBGL || UNITY_EDITOR
        private Native.UnityVideoCapturerFactory mVideoFactory;
        public Native.NativeVideoInput VideoInput
        {
            get
            {
                if(mFactory != null)
                {
                    var factory = mFactory as Byn.Media.Native.NativeWebRtcCallFactory;
                    return factory.VideoInput;
                }
                return null;
            }
        }
#endif

        //android needs a static init process. 
        /// <summary>
        /// True if the platform specific init process was tried
        /// </summary>
        private static bool sStaticInitTried = false;

        /// <summary>
        /// true if the static init process was successful. false if not yet tried or failed.
        /// </summary>
        private static bool sStaticInitSuccessful = false;

        /// <summary>
        /// Set to true to log verbose messages
        /// </summary>
        private static bool sLogVerbose = false;

        private void Awake()
        {
            //make sure the wrapper was initialized
            TryStaticInitialize();
            if (sStaticInitSuccessful == false)
            {
                Debug.LogError("Initialization of the webrtc plugin failed. StaticInitSuccessful is false. ");
                mFactory = null;
                return;
            }


#if UNITY_WEBGL && !UNITY_EDITOR
        
        mFactory = new Byn.Media.Browser.BrowserCallFactory();
#else
            if (INTERNAL_VERBOSE_LOG)
            {
                Byn.Net.Native.NativeWebRtcNetworkFactory.SetNativeLogLevel(WebRtcCSharp.LoggingSeverity.LS_INFO);
                //this will route the log via SLog class to the Unity log. This can cause crashes
                //and doesn't work on all platforms
                //Byn.Net.Native.NativeWebRtcNetworkFactory.SetNativeLogToSLog(WebRtcCSharp.LoggingSeverity.LS_NONE);
            }
            else
            {
                Byn.Net.Native.NativeWebRtcNetworkFactory.SetNativeLogLevel(WebRtcCSharp.LoggingSeverity.LS_NONE);
            }
            try
            {

                Byn.Media.Native.NativeWebRtcCallFactory factory = new Byn.Media.Native.NativeWebRtcCallFactory();
                mFactory = factory;
                

				//old video input system relied on callbacks not supported in IL2CPP
				if(OBSOLETE_UNITY_CAMERA)
				{
#if ENABLE_IL2CPP
					Debug.LogWarning("UnityVideoCapturerFactory isn't supported with IL2CPP");
#else
                    mVideoFactory = new Native.UnityVideoCapturerFactory();
                    factory.AddVideoCapturerFactory(mVideoFactory);
#endif
				}
                

#if UNITY_IOS
				//workaround for WebRTC / Unity audio bug on ios
				//WebRTC will deactivate the audio session once all calls ended
				//This will keep it active as Unity relies on this session as well
				WebRtcCSharp.IosHelper.InitAudioLayer();
#endif

            }
            catch (Exception e)
            {
                Debug.LogError("Failed to create the call factory. This might be because a platform specific " +
                    " dll is missing or set to inactive in the unity editor.");
                Debug.LogException(e);
            }
#endif

            SetDefaultLogger(true, false);
        }
        public void Update()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
        //nothing to do here yet
#else
            if (mVideoFactory != null)
                mVideoFactory.Update();
#endif
        }
        public static void TryStaticInitialize()
        {
            //make sure it is called only once. no need for multiple static inits...
            if (sStaticInitTried)
                return;

            //this library builds on top of the network version -> make sure this one is initialized
            WebRtcNetworkFactory.TryStaticInitialize();
            if (WebRtcNetworkFactory.StaticInitSuccessful == false)
            {
                Debug.LogError("WebRtcNetwork failed to initialize. UnityCallFactory can't be used without WebRtcNetwork!");
                sStaticInitSuccessful = false;
                return;
            }


#if UNITY_WEBGL && !UNITY_EDITOR  //uncomment to be able to run in the editor using the native version

            //check if the java script part is available
            if (Byn.Media.Browser.BrowserMediaNetwork.IsAvailable() == false)
            {
                //js part is missing -> inject the code into the browser
                Byn.Media.Browser.BrowserMediaNetwork.InjectJsCode();
            }
            //if still not available something failed. setting sStaticInitSuccessful to false
            //will block the use of the factories
            sStaticInitSuccessful = Byn.Media.Browser.BrowserMediaNetwork.IsAvailable();
            if(sStaticInitSuccessful == false)
            {
                Debug.LogError("Failed to access the java script library. This might be because of browser incompatibility or a missing java script plugin!");
            }
#else
            sStaticInitSuccessful = true;
#endif
        }
        /// <summary>
        /// Creates a new ICall object.
        /// Only use this method to ensure that your software will keep working on other platforms supported in 
        /// future versions of this library.
        /// </summary>
        /// <param name="config">Network configuration</param>
        /// <returns></returns>
        public ICall Create(NetworkConfig config = null)
        {
            ICall call = mFactory.Create(config);
            if (call == null)
            {
                Debug.LogError("Creation of call object failed. Platform not supported? Platform specific dll not included?");
            }
            return call;
        }
        public IMediaNetwork CreateMediaNetwork(NetworkConfig config)
        {
            return mFactory.CreateMediaNetwork(config);
        }

        /// <summary>
        /// Returns a list containing the names of all available video devices. 
        /// 
        /// They can be used to select a certian device using the class
        /// MediaConfiguration and the method ICall.Configuration.
        /// </summary>
        /// <returns>Returns a list of video devices </returns>
        public string[] GetVideoDevices()
        {
            if (mFactory != null)
                return mFactory.GetVideoDevices();
            return new string[] { };
        }

        /// <summary>
        /// True if the video device can be chosen by the application. False if the environment (the browser usually)
        /// will automatically choose a suitable device.
        /// </summary>
        /// <returns></returns>
        public bool CanSelectVideoDevice()
        {
            if (mFactory != null)
            {
                return mFactory.CanSelectVideoDevice();
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Unity will call this during shutdown. It will make sure all ICall objects and the factory
        /// itself will be destroyed properly.
        /// </summary>
        protected override void OnDestroy()
        {
            Dispose();
            base.OnDestroy();
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!mIsDisposed)
            {
                if (disposing)
                {
                    Debug.Log("UnityCallFactory is being destroyed. All created calls will be destroyed as well!");
                    //cleanup
                    if (mFactory != null)
                    {
                        mFactory.Dispose();
                        mFactory = null;
                    }
                    Debug.Log("Network factory destroyed");
                }
                mIsDisposed = true;
            }
        }

        /// <summary>
        /// Destroys the factory.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }



        /// <summary>
        /// Mobile native only:
        /// Turns on/off the phones speaker
        /// </summary>
        /// <param name="state"></param>
        public void SetLoudspeakerStatus(bool state)
        {
#if UNITY_IOS
			WebRtcCSharp.IosHelper.SetLoudspeakerStatus(state);
#elif UNITY_ANDROID
			//android will just crash if WebRTC's 
			//SetLoudspeakerStatus is used
            //workaround via java
            Byn.Media.Android.AndroidHelper.SetSpeakerOn(state);
#else

            Debug.LogError("GetLoudspeakerStatus is only supported on mobile platforms.");
#endif
        }
        /// <summary>
        /// Checks if the phones speaker is turned on. Only for mobile native platforms
        /// </summary>
        /// <returns></returns>
        public bool GetLoudspeakerStatus()
        {
#if UNITY_IOS
			return WebRtcCSharp.IosHelper.GetLoudspeakerStatus();
#elif UNITY_ANDROID
            //android will just crash if GetLoudspeakerStatus is used
            //workaround via java
            return Byn.Media.Android.AndroidHelper.IsSpeakerOn();
#else
            Debug.LogError("GetLoudspeakerStatus is only supported on mobile platforms.");
            return false;
#endif
        }

        /// <summary>
        /// 
        /// Activates a default logger. This can be
        /// used to monitor the connection process.
        /// 
        /// You can add your own logger by calling 
        /// SLog.SetLogger(LogHandler); directly instead.
        /// </summary>
        public void SetDefaultLogger(bool active, bool verbose = false)
        {
            sLogVerbose = verbose;
            if (active)
            {
                SLog.SetLogger(OnLog);
            }
            else
            {
                SLog.SetLogger(null);
            }
        }

        private static void OnLog(object msg, string[] tags)
        {
            StringBuilder builder = new StringBuilder();
            bool warning = false;
            bool error = false;
            builder.Append("TAGS:[");
            foreach (var v in tags)
            {
                builder.Append(v);
                builder.Append(",");
                if (v == SLog.TAG_ERROR || v == SLog.TAG_EXCEPTION)
                {
                    error = true;
                }
                else if (v == SLog.TAG_WARNING)
                {
                    warning = true;
                }
            }
            builder.Append("]");
            builder.Append(msg);
            if (error)
            {
                LogError(builder.ToString());
            }
            else if (warning)
            {
                LogWarning(builder.ToString());
            }
            else if(sLogVerbose)
            {
                Log(builder.ToString());
            }
        }

        private static void Log(string s)
        {
            if (s.Length > 2048 && Application.platform != RuntimePlatform.Android)
            {
                foreach (string splitMsg in SplitLongMsgs(s))
                {
                    Debug.Log(splitMsg);
                }
            }
            else
            {
                Debug.Log(s);
            }
        }
        private static void LogWarning(string s)
        {
            if (s.Length > 2048 && Application.platform != RuntimePlatform.Android)
            {
                foreach (string splitMsg in SplitLongMsgs(s))
                {
                    Debug.LogWarning(splitMsg);
                }
            }
            else
            {
                Debug.LogWarning(s);
            }
        }
        private static void LogError(string s)
        {
            if (s.Length > 2048 && Application.platform != RuntimePlatform.Android)
            {
                foreach (string splitMsg in SplitLongMsgs(s))
                {
                    Debug.LogError(splitMsg);
                }
            }
            else
            {
                Debug.LogError(s);
            }
        }

        private static string[] SplitLongMsgs(string s)
        {
            const int maxLength = 2048;
            int count = s.Length / maxLength + 1;
            string[] messages = new string[count];
            for (int i = 0; i < count; i++)
            {
                int start = i * maxLength;
                int length = s.Length - start;
                if (length > maxLength)
                    length = maxLength;
                messages[i] = "[" + (i + 1) + "/" + count + "]" + s.Substring(start, length);

            }
            return messages;
        }
    }
}