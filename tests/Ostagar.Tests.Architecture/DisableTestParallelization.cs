using System.Reflection;

// Serialises the test assembly so LawFirepowerTests can plant deliberately non-compliant files
// under src/ without another law scan racing past them and reporting the plant as real drift.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
