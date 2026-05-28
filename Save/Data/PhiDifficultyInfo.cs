using System.Collections;

namespace PhigrosArchive.Save.Data
{
    public class PhiDifficultyInfo<TInfo>
    {
        public TInfo EZ { get; set; } = default!;
        public TInfo HD { get; set; } = default!;
        public TInfo IN { get; set; } = default!;
        public TInfo? AT { get; set; } = default!;
        public TInfo? Legacy { get; set; } = default!;

        public TInfo? GetByIndex(int index) => index switch
        {
            0 => EZ,
            1 => HD,
            2 => IN,
            3 => AT,
            4 => Legacy,
            _ => throw new IndexOutOfRangeException(),
        };

        public Dictionary<string, TInfo> GetDictionary(bool includeLegacy = false)
        {
            var result = new Dictionary<string, TInfo>
            {
                ["EZ"] = EZ,
                ["HD"] = HD,
                ["IN"] = IN,
            };
            if (AT != null) result["AT"] = AT;
            if (includeLegacy && Legacy != null) result["Legacy"] = Legacy;
            return result;
        }

        public IList<TInfo> GetArray(bool includeLegacy = false)
            => GetDictionary(includeLegacy).Values.ToList();

        public PhiDifficultyInfo() { }

        public PhiDifficultyInfo(TInfo ez, TInfo hd, TInfo in_, TInfo? at, TInfo? legacy)
        {
            EZ = ez; HD = hd; IN = in_; AT = at; Legacy = legacy;
        }

        public PhiDifficultyInfo(IEnumerable<TInfo> infos)
        {
            var e = infos.GetEnumerator();
            if (e.MoveNext()) EZ = e.Current;
            if (e.MoveNext()) HD = e.Current;
            if (e.MoveNext()) IN = e.Current;
            AT = e.MoveNext() ? e.Current : default!;
            Legacy = e.MoveNext() ? e.Current : default!;
        }

    }
}
