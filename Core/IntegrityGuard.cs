using System;
using System.Reflection;
using System.Windows.Forms;

namespace VibranceZ.Core
{
    internal static class IntegrityGuard
    {
        public const string BrandAppName = "Z4Vibrance";
        public const string BrandAuthor = "Zodi4c";
        public const string BrandRepoOwner = "zodi4ctvn";
        public const string BrandRepoUrl = "https://github.com/zodi4ctvn";

        public static void Verify()
        {
            if (!VerifyBrandConstants() || !VerifyAssemblyMetadata())
            {
                string message =
                    "[TR] Z4Vibrance marka veya yazar atıf bilgileri değiştirilmiş ya da kaldırılmış.\n" +
                    "Lütfen orijinal Zodi4c marka/atıf bilgilerini geri yükleyin ve yalnızca resmi kaynaktan indirin:\n" +
                    "https://github.com/zodi4ctvn\n\n" +
                    "[EN] Z4Vibrance brand or author attribution metadata has been modified or removed.\n" +
                    "Please restore the original Zodi4c branding/attribution and use the official release from:\n" +
                    "https://github.com/zodi4ctvn";

                try
                {
                    MessageBox.Show(
                        message,
                        "Z4Vibrance — Attribution Integrity Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                catch
                {
                }

                Environment.Exit(1);
            }
        }

        private static bool VerifyBrandConstants()
        {
            if (!string.Equals(BrandAppName, "Z4Vibrance", StringComparison.Ordinal))
                return false;
            if (!string.Equals(BrandAuthor, "Zodi4c", StringComparison.Ordinal))
                return false;
            if (!string.Equals(BrandRepoOwner, "zodi4ctvn", StringComparison.Ordinal))
                return false;
            if (BrandRepoUrl == null || BrandRepoUrl.IndexOf("zodi4ctvn", StringComparison.OrdinalIgnoreCase) < 0)
                return false;
            return true;
        }

        private static bool VerifyAssemblyMetadata()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                if (asm == null)
                    return false;

                var titleAttr = Attribute.GetCustomAttribute(asm, typeof(AssemblyTitleAttribute)) as AssemblyTitleAttribute;
                var productAttr = Attribute.GetCustomAttribute(asm, typeof(AssemblyProductAttribute)) as AssemblyProductAttribute;
                var companyAttr = Attribute.GetCustomAttribute(asm, typeof(AssemblyCompanyAttribute)) as AssemblyCompanyAttribute;
                var copyrightAttr = Attribute.GetCustomAttribute(asm, typeof(AssemblyCopyrightAttribute)) as AssemblyCopyrightAttribute;

                if (titleAttr == null || titleAttr.Title == null ||
                    titleAttr.Title.IndexOf("Z4Vibrance", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                if (productAttr == null || productAttr.Product == null ||
                    productAttr.Product.IndexOf("Z4Vibrance", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                if (companyAttr == null || companyAttr.Company == null ||
                    companyAttr.Company.IndexOf("Zodi4c", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                if (copyrightAttr == null || copyrightAttr.Copyright == null ||
                    copyrightAttr.Copyright.IndexOf("Zodi4c", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
