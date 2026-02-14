using System;
using System.Net;

namespace JoSystem.Services.Hosting
{
    public static class IpWhitelistHelper
    {
        public static bool IsAllowed(IPAddress remoteIp, string whitelistRaw)
        {
            if (remoteIp == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(whitelistRaw))
            {
                return false;
            }

            var entries = whitelistRaw.Split([';', '|', ',', ' '], StringSplitOptions.RemoveEmptyEntries);
            foreach (var entryRaw in entries)
            {
                var entry = entryRaw.Trim();
                if (entry.Length == 0) continue;

                if (entry.Contains('/'))
                {
                    if (IsInCidr(remoteIp, entry))
                    {
                        return true;
                    }
                }
                else if (entry.Contains("-"))
                {
                    if (IsInRange(remoteIp, entry))
                    {
                        return true;
                    }
                }
                else if (entry.Contains("*"))
                {
                    if (IsWildcardMatch(remoteIp, entry))
                    {
                        return true;
                    }
                }
                else
                {
                    if (IPAddress.TryParse(entry, out var allowedIp) && allowedIp.Equals(remoteIp))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsInRange(IPAddress address, string range)
        {
            var parts = range.Split('-');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!IPAddress.TryParse(parts[0].Trim(), out var start))
            {
                return false;
            }

            if (!IPAddress.TryParse(parts[1].Trim(), out var end))
            {
                return false;
            }

            var addressBytes = address.GetAddressBytes();
            var startBytes = start.GetAddressBytes();
            var endBytes = end.GetAddressBytes();

            if (addressBytes.Length != startBytes.Length || addressBytes.Length != endBytes.Length)
            {
                return false;
            }

            if (CompareBytes(startBytes, endBytes) > 0)
            {
                var tmp = startBytes;
                startBytes = endBytes;
                endBytes = tmp;
            }

            return CompareBytes(addressBytes, startBytes) >= 0 && CompareBytes(addressBytes, endBytes) <= 0;
        }

        private static bool IsWildcardMatch(IPAddress address, string pattern)
        {
            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            var addrText = address.ToString();
            var addrParts = addrText.Split('.');
            var patternParts = pattern.Split('.');

            if (addrParts.Length != patternParts.Length)
            {
                return false;
            }

            for (int i = 0; i < addrParts.Length; i++)
            {
                var p = patternParts[i];
                if (p == "*")
                {
                    continue;
                }

                if (!string.Equals(p, addrParts[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static int CompareBytes(byte[] a, byte[] b)
        {
            var length = Math.Min(a.Length, b.Length);
            for (int i = 0; i < length; i++)
            {
                int diff = a[i].CompareTo(b[i]);
                if (diff != 0)
                {
                    return diff;
                }
            }

            return a.Length.CompareTo(b.Length);
        }

        private static bool IsInCidr(IPAddress address, string cidr)
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!IPAddress.TryParse(parts[0], out var baseAddress))
            {
                return false;
            }

            if (!int.TryParse(parts[1], out var prefixLength))
            {
                return false;
            }

            var addressBytes = address.GetAddressBytes();
            var baseBytes = baseAddress.GetAddressBytes();

            if (addressBytes.Length != baseBytes.Length)
            {
                return false;
            }

            int fullBytes = prefixLength / 8;
            int remainingBits = prefixLength % 8;

            for (int i = 0; i < fullBytes; i++)
            {
                if (addressBytes[i] != baseBytes[i])
                {
                    return false;
                }
            }

            if (remainingBits > 0)
            {
                int mask = (byte)~(0xFF >> remainingBits);
                if ((addressBytes[fullBytes] & mask) != (baseBytes[fullBytes] & mask))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
