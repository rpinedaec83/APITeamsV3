namespace APITeamsV3.Application.Common.Models
{
    public class SectionEligibilityResult
    {
        public bool IsEligible { get; set; }
        public string Reason { get; set; } = string.Empty;

        public static SectionEligibilityResult Eligible() => new() { IsEligible = true, Reason = "Elegible" };
        public static SectionEligibilityResult Ineligible(string reason) => new() { IsEligible = false, Reason = reason };
    }
}
