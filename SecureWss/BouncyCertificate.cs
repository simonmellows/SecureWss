﻿using Crestron.SimplSharp;
using C5Debugger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using X509Certificate2 = System.Security.Cryptography.X509Certificates.X509Certificate2;
using X509KeyStorageFlags = System.Security.Cryptography.X509Certificates.X509KeyStorageFlags;
using X509ContentType = System.Security.Cryptography.X509Certificates.X509ContentType;
using System.Text;
using Org.BouncyCastle.Crypto.Operators;
using SecureWss.Websockets;
using System.Threading;

//using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.OpenSsl;


namespace SecureWss
{
    /// <summary>
    /// Taken From https://github.com/rlipscombe/bouncy-castle-csharp/
    /// </summary>
    internal class BouncyCertificate
    {
        public string CertificatePassword { get; set; } = "password";
        public X509Certificate2 LoadCertificate(string issuerFileName, string password)
        {
            // We need to pass 'Exportable', otherwise we can't get the private key.
            var issuerCertificate = new X509Certificate2(issuerFileName, password, X509KeyStorageFlags.Exportable);
            return issuerCertificate;
        }

        // Constructor
        private string SubjectName { get; set; }
        private string[] SubjectAlternativeNames { get; set; }
        private string OutputDirectory { get; set; }
        private string ServerCertName { get; set; }
        private Server WebsocketServer { get; set; }
        public BouncyCertificate(string subjectName, string[] subjectAlternativeNames, string outputDirectory, string serverCertName, Server _websocketServer)
        {
            this.SubjectName = subjectName;
            this.SubjectAlternativeNames = subjectAlternativeNames;
            this.OutputDirectory = outputDirectory;
            this.ServerCertName = serverCertName;
            this.WebsocketServer = _websocketServer;
        }
        private bool Busy { get; set; }

        public X509Certificate2 IssueCertificate(string subjectName, X509Certificate2 issuerCertificate, string[] subjectAlternativeNames, KeyPurposeID[] usages)
        {
            // It's self-signed, so these are the same.
            var issuerName = issuerCertificate.Subject;
            var random = GetSecureRandom();
            var subjectKeyPair = GenerateKeyPair(random, 2048);
            var issuerKeyPair = DotNetUtilities.GetKeyPair(issuerCertificate.PrivateKey);
            var serialNumber = GenerateSerialNumber(random);
            var issuerSerialNumber = new BigInteger(issuerCertificate.GetSerialNumber());
            const bool isCertificateAuthority = false;

            var certificate = GenerateCertificate(random, subjectName, subjectKeyPair, serialNumber,
                                                  subjectAlternativeNames, issuerName, issuerKeyPair,
                                                  issuerSerialNumber, isCertificateAuthority, usages, 1);

            return ConvertCertificate(certificate, subjectKeyPair, random);
        }

        public X509Certificate2 CreateCertificateAuthorityCertificate(string subjectName, string[] subjectAlternativeNames, KeyPurposeID[] usages)
        {
            // It's self-signed, so these are the same.
            var issuerName = subjectName;

            var random = GetSecureRandom();
            var subjectKeyPair = GenerateKeyPair(random, 2048);

            // It's self-signed, so these are the same.
            var issuerKeyPair = subjectKeyPair;

            var serialNumber = GenerateSerialNumber(random);
            var issuerSerialNumber = serialNumber; // Self-signed, so it's the same serial number.

            const bool isCertificateAuthority = true;
            var certificate = GenerateCertificate(random, subjectName, subjectKeyPair, serialNumber,
                                                  subjectAlternativeNames, issuerName, issuerKeyPair,
                                                  issuerSerialNumber, isCertificateAuthority,
                                                  usages, 50);
            return ConvertCertificate(certificate, subjectKeyPair, random);
        }

        public X509Certificate2 CreateSelfSignedCertificate(string subjectName, string[] subjectAlternativeNames, KeyPurposeID[] usages)
        {
            // It's self-signed, so these are the same.
            var issuerName = subjectName;

            var random = GetSecureRandom();
            var subjectKeyPair = GenerateKeyPair(random, 2048);

            // It's self-signed, so these are the same.
            var issuerKeyPair = subjectKeyPair;

            var serialNumber = GenerateSerialNumber(random);
            var issuerSerialNumber = serialNumber; // Self-signed, so it's the same serial number.

            const bool isCertificateAuthority = false;
            var certificate = GenerateCertificate(random, subjectName, subjectKeyPair, serialNumber,
                                                  subjectAlternativeNames, issuerName, issuerKeyPair,
                                                  issuerSerialNumber, isCertificateAuthority,
                                                  usages, 2);
            return ConvertCertificate(certificate, subjectKeyPair, random);
        }

        private SecureRandom GetSecureRandom()
        {
            // Since we're on Windows, we'll use the CryptoAPI one (on the assumption
            // that it might have access to better sources of entropy than the built-in
            // Bouncy Castle ones):
            var randomGenerator = new CryptoApiRandomGenerator();
            var random = new SecureRandom(randomGenerator);
            return random;
        }

        private X509Certificate GenerateCertificate(SecureRandom random,
                                                           string subjectName,
                                                           AsymmetricCipherKeyPair subjectKeyPair,
                                                           BigInteger subjectSerialNumber,
                                                           string[] subjectAlternativeNames,
                                                           string issuerName,
                                                           AsymmetricCipherKeyPair issuerKeyPair,
                                                           BigInteger issuerSerialNumber,
                                                           bool isCertificateAuthority,
                                                           KeyPurposeID[] usages,
                                                           int years)
        {
            var certificateGenerator = new X509V3CertificateGenerator();

            certificateGenerator.SetSerialNumber(subjectSerialNumber);

            var issuerDN = new X509Name(issuerName);
            certificateGenerator.SetIssuerDN(issuerDN);

            // Note: The subject can be omitted if you specify a subject alternative name (SAN).
            var subjectDN = new X509Name(subjectName);
            certificateGenerator.SetSubjectDN(subjectDN);

            // Our certificate needs valid from/to values.
            var notBefore = DateTime.Now;
            var notAfter = notBefore.AddYears(years);

            certificateGenerator.SetNotBefore(notBefore);
            certificateGenerator.SetNotAfter(notAfter);

            // The subject's public key goes in the certificate.
            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            AddAuthorityKeyIdentifier(certificateGenerator, issuerDN, issuerKeyPair, issuerSerialNumber);
            AddSubjectKeyIdentifier(certificateGenerator, subjectKeyPair);

            Debug.Print($"Adding basic constraints. isCertificateAuthority: {isCertificateAuthority}");
            AddBasicConstraints(certificateGenerator, isCertificateAuthority);

            if (usages != null && usages.Any())
                AddExtendedKeyUsage(certificateGenerator, usages);

            if (subjectAlternativeNames != null && subjectAlternativeNames.Any())
                AddSubjectAlternativeNames(certificateGenerator, subjectAlternativeNames);

            // Set the signature algorithm. This is used to generate the thumbprint which is then signed
            // with the issuer's private key. We'll use SHA-256, which is (currently) considered fairly strong.
            const string signatureAlgorithm = "SHA256WithRSA";

            // The certificate is signed with the issuer's private key.
            ISignatureFactory signatureFactory = new Asn1SignatureFactory(signatureAlgorithm, issuerKeyPair.Private, random);
            var certificate = certificateGenerator.Generate(signatureFactory);
            return certificate;
        }

        /// <summary>
        /// The certificate needs a serial number. This is used for revocation,
        /// and usually should be an incrementing index (which makes it easier to revoke a range of certificates).
        /// Since we don't have anywhere to store the incrementing index, we can just use a random number.
        /// </summary>
        /// <param name="random"></param>
        /// <returns></returns>
        private BigInteger GenerateSerialNumber(SecureRandom random)
        {
            var serialNumber =
                BigIntegers.CreateRandomInRange(
                    BigInteger.One, BigInteger.ValueOf(Int64.MaxValue), random);
            return serialNumber;
        }

        /// <summary>
        /// Generate a key pair.
        /// </summary>
        /// <param name="random">The random number generator.</param>
        /// <param name="strength">The key length in bits. For RSA, 2048 bits should be considered the minimum acceptable these days.</param>
        /// <returns></returns>
        private AsymmetricCipherKeyPair GenerateKeyPair(SecureRandom random, int strength)
        {
            var keyGenerationParameters = new KeyGenerationParameters(random, strength);

            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            var subjectKeyPair = keyPairGenerator.GenerateKeyPair();
            return subjectKeyPair;
        }

        /// <summary>
        /// Add the Authority Key Identifier. According to http://www.alvestrand.no/objectid/2.5.29.35.html, this
        /// identifies the public key to be used to verify the signature on this certificate.
        /// In a certificate chain, this corresponds to the "Subject Key Identifier" on the *issuer* certificate.
        /// The Bouncy Castle documentation, at http://www.bouncycastle.org/wiki/display/JA1/X.509+Public+Key+Certificate+and+Certification+Request+Generation,
        /// shows how to create this from the issuing certificate. Since we're creating a self-signed certificate, we have to do this slightly differently.
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="issuerDN"></param>
        /// <param name="issuerKeyPair"></param>
        /// <param name="issuerSerialNumber"></param>
        private void AddAuthorityKeyIdentifier(X509V3CertificateGenerator certificateGenerator,
                                                      X509Name issuerDN,
                                                      AsymmetricCipherKeyPair issuerKeyPair,
                                                      BigInteger issuerSerialNumber)
        {
            var authorityKeyIdentifierExtension =
                new AuthorityKeyIdentifier(
                    SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(issuerKeyPair.Public),
                    new GeneralNames(new GeneralName(issuerDN)),
                    issuerSerialNumber);
            certificateGenerator.AddExtension(
                X509Extensions.AuthorityKeyIdentifier.Id, false, authorityKeyIdentifierExtension);
        }

        /// <summary>
        /// Add the "Subject Alternative Names" extension. Note that you have to repeat
        /// the value from the "Subject Name" property.
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="subjectAlternativeNames"></param>
        private void AddSubjectAlternativeNames(X509V3CertificateGenerator certificateGenerator,
                                                       IEnumerable<string> subjectAlternativeNames)
        {
            /*var subjectAlternativeNamesExtension =
                new DerSequence(
                    subjectAlternativeNames.Select(name => new GeneralName(GeneralName.DnsName, name))
                                           .ToArray<Asn1Encodable>());

            certificateGenerator.AddExtension(
                X509Extensions.SubjectAlternativeName.Id, false, subjectAlternativeNamesExtension);*/
            var generalNames = new List<GeneralName>();

            foreach (var name in subjectAlternativeNames)
            {
                if (System.Net.IPAddress.TryParse(name, out var ipAddress))
                {
                    // It's an IP address
                    generalNames.Add(new GeneralName(GeneralName.IPAddress, name));
                }
                else
                {
                    // It's a DNS name
                    generalNames.Add(new GeneralName(GeneralName.DnsName, name));
                }
            }

            var subjectAlternativeNamesExtension = new DerSequence(generalNames.ToArray<Asn1Encodable>());
            certificateGenerator.AddExtension(X509Extensions.SubjectAlternativeName.Id, false, subjectAlternativeNamesExtension);
        }

        /// <summary>
        /// Add the "Extended Key Usage" extension, specifying (for example) "server authentication".
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="usages"></param>
        private void AddExtendedKeyUsage(X509V3CertificateGenerator certificateGenerator, KeyPurposeID[] usages)
        {
            certificateGenerator.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id, false, new ExtendedKeyUsage(usages));
        }

        /// <summary>
        /// Add the "Basic Constraints" extension.
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="isCertificateAuthority"></param>
        private void AddBasicConstraints(X509V3CertificateGenerator certificateGenerator,
                                                bool isCertificateAuthority)
        {
            certificateGenerator.AddExtension(
                X509Extensions.BasicConstraints.Id, true, new BasicConstraints(isCertificateAuthority));
        }

        /// <summary>
        /// Add the Subject Key Identifier.
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="subjectKeyPair"></param>
        private void AddSubjectKeyIdentifier(X509V3CertificateGenerator certificateGenerator,
                                                    AsymmetricCipherKeyPair subjectKeyPair)
        {
            var subjectKeyIdentifierExtension =
                new SubjectKeyIdentifier(
                    SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(subjectKeyPair.Public));
            certificateGenerator.AddExtension(
                X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifierExtension);
        }

        private X509Certificate2 ConvertCertificate(X509Certificate certificate,
                                                           AsymmetricCipherKeyPair subjectKeyPair,
                                                           SecureRandom random)
        {
            // Now to convert the Bouncy Castle certificate to a .NET certificate.
            // See http://web.archive.org/web/20100504192226/http://www.fkollmann.de/v2/post/Creating-certificates-using-BouncyCastle.aspx
            // ...but, basically, we create a PKCS12 store (a .PFX file) in memory, and add the public and private key to that.
            var store = new Pkcs12Store();

            // What Bouncy Castle calls "alias" is the same as what Windows terms the "friendly name".
            string friendlyName = certificate.SubjectDN.ToString();

            // Add the certificate.
            var certificateEntry = new X509CertificateEntry(certificate);
            store.SetCertificateEntry(friendlyName, certificateEntry);

            // Add the private key.
            store.SetKeyEntry(friendlyName, new AsymmetricKeyEntry(subjectKeyPair.Private), new[] { certificateEntry });

            // Convert it to an X509Certificate2 object by saving/loading it from a MemoryStream.
            // It needs a password. Since we'll remove this later, it doesn't particularly matter what we use.

            var stream = new MemoryStream();
            store.Save(stream, CertificatePassword.ToCharArray(), random);

            var convertedCertificate =
                new X509Certificate2(stream.ToArray(),
                                     CertificatePassword,
                                     X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);
            return convertedCertificate;
        }

        public void WriteCertificate(X509Certificate2 certificate, string outputDirectory, string certName)
        {
            // This password is the one attached to the PFX file. Use 'null' for no password.
            // Create PFX (PKCS #12) with private key
            string serverDirectory = @"\user\html\";

            try
            {
                var pfx = certificate.Export(X509ContentType.Pfx, CertificatePassword);
                File.WriteAllBytes($"{Path.Combine(outputDirectory, certName)}.pfx", pfx);
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, $"Failed to write x509 cert pfx\r\n{ex.Message}");
                ErrorLog.Error($"Failed to write x509 cert pfx\r\n{ex.Message}");
            }
            // Create Base 64 encoded CER (public key only)
            using (var writer = new StreamWriter($"{Path.Combine(outputDirectory, certName)}.cer", false))
            {
                try
                {
                    var contents = $"-----BEGIN CERTIFICATE-----\r\n{Convert.ToBase64String(certificate.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks)}\r\n-----END CERTIFICATE-----";
                    writer.Write(contents);
                }
                catch (Exception ex)
                {
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, $"Failed to write x509 cert cer\r\n{ex.Message}");
                    ErrorLog.Error($"Failed to write x509 cert cer\r\n{ex.Message}");
                }
            }
        }
        public bool AddCertToStore(X509Certificate2 cert, System.Security.Cryptography.X509Certificates.StoreName st, System.Security.Cryptography.X509Certificates.StoreLocation sl)
        {
            bool bRet = false;

            try
            {
                var store = new System.Security.Cryptography.X509Certificates.X509Store(st, sl);
                store.Open(System.Security.Cryptography.X509Certificates.OpenFlags.ReadWrite);
                store.Add(cert);

                store.Close();
                bRet = true;
            }
            catch (Exception ex)
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, $"AddCertToStore Failed\r\n{ex.Message}\r\n{ex.StackTrace}");
                ErrorLog.Error($"AddCertToStore Failed\r\n{ex.Message}\r\n{ex.StackTrace}");
            }

            return bRet;
        }

        public void CreateAndWriteCertificates(string subjectName, string[] subjectAlternativeNames, string outputDirectory, string serverCertName, Server _websocketServer)
        {
            string rootCertPath = $"{Path.Combine(outputDirectory, Constants.RootCertName)}";
            string serverCertPath = $"{Path.Combine(outputDirectory, serverCertName)}";
            X509Certificate2 RootCert;
            Busy = true;

            try
            {
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, $"Root cert creation/retrieval starting...");
                // If the root certificate doesn't already exist
                if (!File.Exists($"{rootCertPath}.pfx"))
                {
                    // Create the root certificate
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, $"Creating new root certificate...");
                    RootCert = CreateCertificateAuthorityCertificate("CN=Cornflake", null, new[] { KeyPurposeID.IdKPServerAuth, KeyPurposeID.IdKPClientAuth });
                    WriteCertificate(RootCert, outputDirectory, Constants.RootCertName);
                }
                else
                {
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, $"Getting existing root certificate...");
                    RootCert = new X509Certificate2($"{rootCertPath}.pfx", CertificatePassword, X509KeyStorageFlags.Exportable);
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, $"Root certificate retrieved. Expiry date: {RootCert.NotAfter}");
                }

                if (RootCert == null)
                {
                    if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, $"ERROR: No root certificate");
                    throw new Exception("ERROR: No root certificate");
                }
                if (Constants.EnableDebugging) Debug.Print(DebugLevel.Debug, $"Root cert creation/retrieval complete.");

                // If the server certificate doesn't already exist
                if (!File.Exists($"{serverCertPath}.pfx"))
                {
                    // Create the server certificate signed by the root certificate
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print(DebugLevel.Debug, $"Creating new server certificate and signing with root certificate.");
                    }
                    var serverCert = IssueCertificate(subjectName, RootCert, subjectAlternativeNames, new[] { KeyPurposeID.IdKPServerAuth, KeyPurposeID.IdKPClientAuth });
                    WriteCertificate(serverCert, outputDirectory, serverCertName);
                    if (Constants.EnableDebugging)
                    {
                        Debug.Print(DebugLevel.Debug, $"New server certificate created.");
                    }

                    // Restart the web server if it's running
                    if (_websocketServer.HttpsIsRunning)
                    {
                        _websocketServer.Restart(Constants.HttpsPort);
                    }
                    else
                    {
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print(DebugLevel.Debug, $"Web server is not running.");
                        }
                    }
                }
                // Otherwise check its date
                else
                {
                    var serverCert = new X509Certificate2($"{serverCertPath}.pfx", CertificatePassword, X509KeyStorageFlags.Exportable);
                    // If the certificate is out of date
                    if (serverCert.NotAfter < DateTime.Now)
                    {
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print(DebugLevel.Debug, $"Certificate '{serverCertPath}.pfx' has expired. Expiry date: {serverCert.NotAfter}");
                        }
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print(DebugLevel.Debug, $"Creating new server certificate and signing with root certificate.");
                        }
                        var newServerCert = IssueCertificate(subjectName, RootCert, subjectAlternativeNames, new[] { KeyPurposeID.IdKPServerAuth, KeyPurposeID.IdKPClientAuth });
                        WriteCertificate(newServerCert, outputDirectory, serverCertName);
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print(DebugLevel.Debug, $"New server certificate created.");
                        }

                        // Restart the web server if it's running
                        if (_websocketServer.HttpsIsRunning)
                        {
                            _websocketServer.Restart(Constants.HttpsPort);
                        }
                        else
                        {
                            if (Constants.EnableDebugging)
                            {
                                Debug.Print(DebugLevel.Debug, $"Web server is not running.");
                            }
                        }
                    }
                    // Otherwise if it's within 3 days of expiry
                    else if (serverCert.NotAfter < DateTime.Now.AddDays(3))
                    {
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print(DebugLevel.Debug, $"Certificate '{serverCertPath}.pfx' expires within 3 days. Expiry date: {serverCert.NotAfter}");
                            Debug.Print(DebugLevel.Debug, $"Creating new server certificate and signing with root certificate.");
                        }
                        var newServerCert = IssueCertificate(subjectName, RootCert, subjectAlternativeNames, new[] { KeyPurposeID.IdKPServerAuth, KeyPurposeID.IdKPClientAuth });
                        WriteCertificate(newServerCert, outputDirectory, serverCertName);
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print(DebugLevel.Debug, $"New server certificate created.");
                        }

                        // Restart the web server if it's running
                        if (_websocketServer.HttpsIsRunning)
                        {
                            _websocketServer.Restart(Constants.HttpsPort);
                        }
                        else
                        {
                            if (Constants.EnableDebugging)
                            {
                                Debug.Print(DebugLevel.Debug, $"Web server is not running.");
                            }
                        }
                    }
                    // Otherwise if its valid
                    else
                    {
                        if (Constants.EnableDebugging)
                        {
                            Debug.Print(DebugLevel.Debug, $"Certificate '{serverCertPath}' is valid. Expiry date: {serverCert.NotAfter}");
                        }
                    }
                }
                Busy = false;
            }
            catch (Exception ex)
            {
                Debug.Print(DebugLevel.Error, $"Failed to create and write certificates\r\n{ex.Message}");
                ErrorLog.Error($"Failed to create and write certificates\r\n{ex.Message}");
                Busy = false;
            }
        }
        public void CheckCertificates()
        {
            // Check certificates every 12 hours
            Timer timer = new Timer(Callback, null, TimeSpan.Zero, TimeSpan.FromHours(12));
        }
        public void Callback(object state)
        {
            if (!Busy)
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.Debug, $"Check certificates method callback called.");
                }
                CreateAndWriteCertificates(SubjectName, SubjectAlternativeNames, OutputDirectory, ServerCertName, WebsocketServer);

            }
            else
            {
                if (Constants.EnableDebugging)
                {
                    Debug.Print(DebugLevel.Debug, $"Bouncy is busy...");
                }
            }
        }
    }
}