using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace JoSystem.Services.Hosting
{
    public static class CertificateHelper
    {
        private const string CaSubjectName = "CN=Yueer.wang, O=J.W, OU=MAPA";
        private const string ServerSubjectName = "CN=JoSystem, O=HCAI, OU=HuaFon";

        public static X509Certificate2 GetOrGenerateCertificate()
        {
            var caCert = GetOrGenerateCACertificate();
            TrustCertificate(caCert);
            return GetOrGenerateServerCertificate(caCert);
        }

        private static X509Certificate2 GetOrGenerateCACertificate()
        {
            var existingCA = FindValidCertificate(StoreName.My, StoreLocation.LocalMachine, CaSubjectName, null)
                             ?? FindValidCertificate(StoreName.My, StoreLocation.CurrentUser, CaSubjectName, null);

            if (existingCA != null) return existingCA;
            return GenerateCACertificate();
        }

        private static X509Certificate2 GetOrGenerateServerCertificate(X509Certificate2 caCert)
        {
            var existingServer = FindValidCertificate(StoreName.My, StoreLocation.LocalMachine, ServerSubjectName, caCert.Subject)
                                 ?? FindValidCertificate(StoreName.My, StoreLocation.CurrentUser, ServerSubjectName, caCert.Subject);

            if (existingServer != null) return existingServer;
            return GenerateServerCertificate(caCert);
        }

        private static X509Certificate2 FindValidCertificate(StoreName storeName, StoreLocation location, string subjectMatch, string issuerMatch)
        {
            try
            {
                using var store = new X509Store(storeName, location);
                store.Open(OpenFlags.ReadOnly);

                string searchKey = subjectMatch.Split(',')[0].Trim();
                if (searchKey.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                {
                    searchKey = searchKey.Substring(3);
                }

                var certs = store.Certificates.Find(X509FindType.FindBySubjectName, searchKey, false);
                foreach (var cert in certs)
                {
                    if (DateTime.Now < cert.NotBefore || DateTime.Now.AddDays(30) > cert.NotAfter) continue;
                    if (issuerMatch != null && cert.Issuer != issuerMatch) continue;
                    if (!cert.HasPrivateKey) continue;

                    var expectedParts = subjectMatch.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    bool match = true;
                    foreach (var part in expectedParts)
                    {
                        if (!cert.Subject.Contains(part.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            match = false;
                            break;
                        }
                    }
                    if (!match) continue;

                    return cert;
                }
            }
            catch { }
            return null;
        }

        private static X509Certificate2 GenerateCACertificate()
        {
            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(
                    CaSubjectName,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));

                var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddYears(20));

                return SaveToStore(certificate, "password", StoreName.My);
            }
        }

        private static X509Certificate2 GenerateServerCertificate(X509Certificate2 caCert)
        {
            using (RSA rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(
                    ServerSubjectName,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                var subjectAlternativeName = new SubjectAlternativeNameBuilder();
                subjectAlternativeName.AddDnsName("localhost");
                subjectAlternativeName.AddIpAddress(IPAddress.Loopback);
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                        {
                            if (ip.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                subjectAlternativeName.AddIpAddress(ip.Address);
                        }
                    }
                }
                request.CertificateExtensions.Add(subjectAlternativeName.Build());

                request.CertificateExtensions.Add(new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

                var serialNumber = BitConverter.GetBytes(DateTime.Now.Ticks);
                var certificate = request.Create(
                    caCert,
                    DateTimeOffset.Now.AddDays(-1),
                    DateTimeOffset.Now.AddYears(10),
                    serialNumber);

                var certWithKey = certificate.CopyWithPrivateKey(rsa);

                return SaveToStore(certWithKey, "password", StoreName.My);
            }
        }

        private static X509Certificate2 SaveToStore(X509Certificate2 cert, string password, StoreName storeName)
        {
            var pfxBytes = cert.Export(X509ContentType.Pfx, password);
            var loadedCert = X509CertificateLoader.LoadPkcs12(
                   pfxBytes,
                   password,
                   X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            try
            {
                using var store = new X509Store(storeName, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                store.Add(loadedCert);
            }
            catch
            {
                using var store = new X509Store(storeName, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                store.Add(loadedCert);
            }
            return loadedCert;
        }

        private static void TrustCertificate(X509Certificate2 caCert)
        {
            try
            {
                using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                InstallCertToStore(store, caCert);
            }
            catch
            {
                using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                InstallCertToStore(store, caCert);
            }
        }

        private static void InstallCertToStore(X509Store store, X509Certificate2 cert)
        {
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false);
            if (found.Count == 0)
            {
                store.Add(cert);
            }

            CleanupOldCertificates(store);
        }

        private static void CleanupOldCertificates(X509Store store)
        {
            try
            {
                var legacyNames = new[] { "JoSystem", "FileServerWPF", "Yueer" };

                foreach (var legacyName in legacyNames)
                {
                    var oldCerts = store.Certificates.Find(X509FindType.FindBySubjectName, legacyName, false);
                    foreach (var old in oldCerts)
                    {
                        if ((old.Subject.Contains("CN=JoSystem") || old.Subject.Contains("CN=FileServerWPF")) && old.Issuer == old.Subject)
                        {
                            try { store.Remove(old); } catch { }
                        }
                        else if (old.Subject.Contains("CN=Yueer") && !CaSubjectName.Contains("CN=Yueer"))
                        {
                            try { store.Remove(old); } catch { }
                        }
                    }
                }
            }
            catch { }
        }
    }
}

