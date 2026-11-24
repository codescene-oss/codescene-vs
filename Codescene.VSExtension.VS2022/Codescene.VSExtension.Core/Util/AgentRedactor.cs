// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Util
{
    internal class AgentRedactor
    {
        private readonly string[] _secrets;

        public AgentRedactor(IEnumerable<string> secrets)
        {
            _secrets = secrets?.Where(s => !string.IsNullOrEmpty(s)).ToArray() ?? Array.Empty<string>();
        }

        public string Redact(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var result = SensitiveDataRedactor.RedactSecrets(value);

            foreach (var secret in _secrets)
            {
                if (result.Contains(secret))
                {
                    result = result.Replace(secret, "***");
                }
            }

            return result;
        }
    }
}
