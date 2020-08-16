using System.Collections;
using System.Collections.Generic;
using Yolol.Execution;

namespace YololCompetition.Services.Execute
{
    public class DefaultValueDeviceNetwork
        : IDeviceNetwork, IEnumerable<(string, Value)>
    {
        private readonly Dictionary<string, IVariable> _saved = new Dictionary<string, IVariable>();

        public IVariable Get(string name)
        {
            if (!_saved.TryGetValue(name, out var v))
            {
                v = new Variable { Value = 0 };
                _saved.Add(name, v);
            }

            return v;
        }

        public IEnumerator<(string, Value)> GetEnumerator()
        {
            foreach (var (key, value) in _saved)
                yield return (key, value.Value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
