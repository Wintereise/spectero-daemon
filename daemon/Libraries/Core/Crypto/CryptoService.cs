﻿/*
    Spectero Daemon - Daemon Component to the Spectero Solution
    Copyright (C)  2017 Spectero, Inc.

    Spectero Daemon is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Spectero Daemon is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.
    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://github.com/ProjectSpectero/daemon/blob/master/LICENSE>.
*/
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.IdentityModel.Tokens;
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
using ServiceStack;
using ServiceStack.OrmLite;
using Spectero.daemon.Libraries.Config;
using Spectero.daemon.Libraries.Core.Constants;
using Spectero.daemon.Libraries.Core.Identity;
using Spectero.daemon.Models;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace Spectero.daemon.Libraries.Core.Crypto
{
    /*
     * Much of the intelligence that went into generating this class was taken from http://blog.differentpla.net/blog/2013/03/24/bouncy-castle-being-a-certificate-authority
     * Originally by Roger Lipscombe.
     * Further adopted into a injectable service by Paul <paul@spectero.com>
     */

    /*
     * Howto use:
     *      var ca = CreateCertificateAuthorityCertificate("CN=ca.spectero.com", null, null);
     *      var crt = IssueCertificate("CN=svr.spectero.com", ca, null, new[] { KeyPurposeID.IdKPServerAuth });
     */

    public class CryptoService : ICryptoService
    {
        private readonly IDbConnection _db;
        private readonly IIdentityProvider _identityProvider;
        private SymmetricSecurityKey jwtKey;

        public CryptoService(IDbConnection db, IIdentityProvider identityProvider)
        {
            _db = db;
            _identityProvider = identityProvider;
        }

        public SymmetricSecurityKey GetJWTSigningKey()
        {
            if (jwtKey != null)
                return jwtKey;
            var stringKey = _db.Single<Configuration>(x => x.Key == ConfigKeys.JWTSymmetricSecurityKey).Value;
            jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(stringKey));
            return jwtKey;
        }

        public X509Certificate2 LoadDatabaseCertificate(string configKey, string passwordKey)
        {
            var storedConfig = ConfigUtils.GetConfig(_db, configKey).Result;

            string storedConfigPassword = null;
            if (passwordKey != null)
                storedConfigPassword = ConfigUtils.GetConfig(_db, passwordKey).Result.Value;

            return LoadCertificate(Convert.FromBase64String(storedConfig.Value), storedConfigPassword);
        }

        public X509Certificate2 LoadCertificate(string issuerFileName, string password = null)
        {
            // We need to pass 'Exportable', otherwise we can't get the private key.
            return new X509Certificate2(issuerFileName, password, X509KeyStorageFlags.Exportable);
        }

        public X509Certificate2 LoadCertificate(byte[] certBytes, string password = null)
        {
            return new X509Certificate2(certBytes, password, X509KeyStorageFlags.Exportable);
        }

        public byte[] GetCertificateBytes(X509Certificate2 certificate, string password = null)
        {
            return certificate.Export(X509ContentType.Pkcs12, password);
        }

        public void WriteCertificate(X509Certificate2 certificate, string outputFileName, string password = null)
        {
            // This password is the one attached to the PFX file. Use 'null' for no password.
            File.WriteAllBytes(outputFileName, GetCertificateBytes(certificate, password));
        }

        public byte[] IssueUserChain(string userAuthKey, KeyPurposeID[] usages, string password = null)
        {
            var caConfig = _db.Select<Configuration>(x => x.Key.Contains("crypto.ca."));
            string caBlob = "", caPassword = "";
            foreach (var config in caConfig)
            {
                switch (config.Key)
                {
                    case ConfigKeys.CertificationAuthority:
                        caBlob = config.Value;
                        break;
                    case ConfigKeys.CeritificationAuthorityPassword:
                        caPassword = config.Value;
                        break;
                }
            }

            if (caBlob.IsEmpty() || caPassword.IsEmpty())
                throw new CryptoException("Could not resolve CA from datastore, please validate your config.");

            var caBytes = Convert.FromBase64String(caBlob);
            var ca = LoadCertificate(caBytes, caPassword);

            var subjectName = "CN=" + userAuthKey;
            
            var userCert = IssueCertificate(subjectName, ca, null, usages, password);

            return ExportCertificateChain(userCert, ca, password);
        }

        public byte[] ExportCertificateChain(X509Certificate2 cert, X509Certificate2 ca, string storePassword = null)
        {
            var collection = new X509Certificate2Collection {new X509Certificate2(ca.RawData), cert};
            return collection.Export(X509ContentType.Pkcs12, storePassword);
        }

        public byte[] ExportCertificateChain(X509Certificate2 ca, X509Certificate2[] certificates,
            string storePassword = null)
        {
            var collection = new X509Certificate2Collection
            {
                new X509Certificate2(ca.RawData)
            };
            collection.AddRange(certificates);

            return collection.Export(X509ContentType.Pkcs12, storePassword);

        }


        public X509Certificate2 IssueCertificate(string subjectName, X509Certificate2 issuerCertificate, string[] subjectAlternativeNames, KeyPurposeID[] extendedKeyUsages, string password = null, KeyUsage usage = null)
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
                                                  issuerSerialNumber, isCertificateAuthority,
                                                  extendedKeyUsages, usage);
            
            return ConvertCertificate(certificate, subjectKeyPair, random, password);
        }

        public byte[] CreateCertificateAuthority(string subjectName, string[] alternativeNames,
            KeyPurposeID[] usages, string password = null)
        {
            return GetCertificateBytes(
                CreateCertificateAuthorityCertificate(
                    subjectName, alternativeNames, usages, password
                ),
                password);
        }

        public X509Certificate2 CreateCertificateAuthorityCertificate(string subjectName, string[] subjectAlternativeNames, KeyPurposeID[] extendedKeyUsages, string password = null)
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
                                                  extendedKeyUsages);
            return ConvertCertificate(certificate, subjectKeyPair, random, password);
        }

        public X509Certificate2 CreateSelfSignedCertificate(string subjectName, string[] subjectAlternativeNames, KeyPurposeID[] extendedKeyUsages, string password = null)
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
                                                  extendedKeyUsages);
            return ConvertCertificate(certificate, subjectKeyPair, random, password);
        }

        public SecureRandom GetSecureRandom()
        {
            // TODO: Figure out if CryptoAPI actually works on Unix
            // Since we're on Windows, we'll use the CryptoAPI one (on the assumption
            // that it might have access to better sources of entropy than the built-in
            // Bouncy Castle ones):
            var randomGenerator = new CryptoApiRandomGenerator();
            var random = new SecureRandom(randomGenerator);
            return random;
        }

        public X509Certificate GenerateCertificate(SecureRandom random,
                                                           string subjectName,
                                                           AsymmetricCipherKeyPair subjectKeyPair,
                                                           BigInteger subjectSerialNumber,
                                                           string[] subjectAlternativeNames,
                                                           string issuerName,
                                                           AsymmetricCipherKeyPair issuerKeyPair,
                                                           BigInteger issuerSerialNumber,
                                                           bool isCertificateAuthority,
                                                           KeyPurposeID[] extendedUsages,
                                                           KeyUsage usage = null)
        {
            var certificateGenerator = new X509V3CertificateGenerator();

            certificateGenerator.SetSerialNumber(subjectSerialNumber);

            // Set the signature algorithm. This is used to generate the thumbprint which is then signed
            // with the issuer's private key. We'll use SHA-256, which is (currently) considered fairly strong.
            const string signatureAlgorithm = "SHA256WithRSA";
            certificateGenerator.SetSignatureAlgorithm(signatureAlgorithm);

            var issuerDN = new X509Name(issuerName);
            certificateGenerator.SetIssuerDN(issuerDN);

            // Note: The subject can be omitted if you specify a subject alternative name (SAN).
            var subjectDN = new X509Name(subjectName);
            certificateGenerator.SetSubjectDN(subjectDN);

            // Our certificate needs valid from/to values.
            var notBefore = DateTime.UtcNow.Date;
            var notAfter = notBefore.AddYears(10);

            certificateGenerator.SetNotBefore(notBefore);
            certificateGenerator.SetNotAfter(notAfter);

            // The subject's public key goes in the certificate.
            certificateGenerator.SetPublicKey(subjectKeyPair.Public);

            // This breaks validation due to reversed serial numbers.
            // See https://security.stackexchange.com/questions/188605/openssl-unable-to-verify-certificate-issued-by-local-ca
            // TODO: Come back to fix this someday.
            //AddAuthorityKeyIdentifier(certificateGenerator, issuerDN, issuerKeyPair, issuerSerialNumber);
            
            AddSubjectKeyIdentifier(certificateGenerator, subjectKeyPair);
            AddBasicConstraints(certificateGenerator, isCertificateAuthority);

            if (extendedUsages != null && extendedUsages.Any())
                AddExtendedKeyUsage(certificateGenerator, extendedUsages);

            if (subjectAlternativeNames != null && subjectAlternativeNames.Any())
                AddSubjectAlternativeNames(certificateGenerator, subjectAlternativeNames);

            // This is what makes the subsequent validation pass.
            if (isCertificateAuthority)
                AddKeyUsages(certificateGenerator, new KeyUsage(KeyUsage.CrlSign | KeyUsage.DigitalSignature | KeyUsage.KeyCertSign | KeyUsage.KeyEncipherment ));
            else if (usage != null)
                AddKeyUsages(certificateGenerator, usage);

            // The certificate is signed with the issuer's private key.
            var certificate = certificateGenerator.Generate(issuerKeyPair.Private, random);
            return certificate;
        }

        /// <summary>
        /// The certificate needs a serial number. This is used for revocation,
        /// and usually should be an incrementing index (which makes it easier to revoke a range of certificates).
        /// Since we don't have anywhere to store the incrementing index, we can just use a random number.
        /// </summary>
        /// <param name="random"></param>
        /// <returns></returns>
        public BigInteger GenerateSerialNumber(SecureRandom random)
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
        public AsymmetricCipherKeyPair GenerateKeyPair(SecureRandom random, int strength)
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
        public void AddAuthorityKeyIdentifier(X509V3CertificateGenerator certificateGenerator,
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
        public void AddSubjectAlternativeNames(X509V3CertificateGenerator certificateGenerator,
                                                       IEnumerable<string> subjectAlternativeNames)
        {
            var subjectAlternativeNamesExtension =
                new DerSequence(
                    subjectAlternativeNames.Select(name => new GeneralName(GeneralName.DnsName, name))
                                           .ToArray<Asn1Encodable>());

            certificateGenerator.AddExtension(
                X509Extensions.SubjectAlternativeName.Id, false, subjectAlternativeNamesExtension);
        }


        /// <summary>
        /// Add the "Key Usage" extension, specifying (for example) "certificate signing".
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="encodedKeyUsage"></param>
        public static void AddKeyUsages(X509V3CertificateGenerator certificateGenerator, KeyUsage encodedKeyUsage) => certificateGenerator.AddExtension(X509Extensions.KeyUsage.Id, true, encodedKeyUsage);

        /// <summary>
        /// Add the "Extended Key Usage" extension, specifying (for example) "server authentication".
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="usages"></param>
        public void AddExtendedKeyUsage(X509V3CertificateGenerator certificateGenerator, KeyPurposeID[] usages)
        {
            certificateGenerator.AddExtension(
                X509Extensions.ExtendedKeyUsage.Id, false, new ExtendedKeyUsage(usages));
        }

        /// <summary>
        /// Add the "Basic Constraints" extension.
        /// </summary>
        /// <param name="certificateGenerator"></param>
        /// <param name="isCertificateAuthority"></param>
        public void AddBasicConstraints(X509V3CertificateGenerator certificateGenerator,
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
        public void AddSubjectKeyIdentifier(X509V3CertificateGenerator certificateGenerator,
                                                    AsymmetricCipherKeyPair subjectKeyPair)
        {
            var subjectKeyIdentifierExtension =
                new SubjectKeyIdentifier(
                    SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(subjectKeyPair.Public));
            certificateGenerator.AddExtension(
                X509Extensions.SubjectKeyIdentifier.Id, false, subjectKeyIdentifierExtension);
        }

        public X509Certificate2 ConvertCertificate(X509Certificate certificate,
                                                           AsymmetricCipherKeyPair subjectKeyPair,
                                                           SecureRandom random, string password)
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

            var temporaryPassword = password ?? PasswordUtils.GeneratePassword(12, 6);

            // Convert it to an X509Certificate2 object by saving/loading it from a MemoryStream.
            // It needs a password. Since we'll remove this later, it doesn't particularly matter what we use.
            var stream = new MemoryStream();
            store.Save(stream, temporaryPassword.ToCharArray(), random);

            var convertedCertificate =
                new X509Certificate2(stream.ToArray(),
                                     temporaryPassword,
                                     X509KeyStorageFlags.Exportable);
            return convertedCertificate;
        }
    }
}