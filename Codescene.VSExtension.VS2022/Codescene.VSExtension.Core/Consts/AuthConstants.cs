// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Consts
{
    public class AuthConstants
    {
        public class States
        {
            public const string SIGNEDOUT = "signed-out";
            public const string SIGNINGIN = "signing-in";
            public const string SIGNEDIN = "signed-in";
        }

        public class Surfaces
        {
            public const string MONITOR = "monitor";
            public const string AUTH = "auth";
        }

        public class Steps
        {
            public const string SUMMARY = "summary";
            public const string RESOLVING = "resolving";
            public const string PICKACCOUNT = "pick-account";
            public const string PICKPROJECT = "pick-project";
        }

        public class Contexts
        {
            public const string SIGNIN = "sign-in";
            public const string MANUAL = "manual";
        }

        public class FlowKinds
        {
            public const string IDLE = "idle";
            public const string SIGNIN = "sign-in";
            public const string MANUALACCOUNT = "manual-account";
            public const string MANUALPROJECT = "manual-project";
        }
    }
}
