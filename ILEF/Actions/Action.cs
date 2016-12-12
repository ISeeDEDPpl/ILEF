
namespace ILEF.Actions
{
    using System.Collections.Generic;
    using System.Linq;
    using global::ILEF.States;

    public class Action
    {
        public Action()
        {
            Parameters = new Dictionary<string, List<string>>();
        }

        public ActionState State { get; set; }

        public Dictionary<string, List<string>> Parameters { get; private set; }

        public void AddParameter(string parameter, string value)
        {
            if (string.IsNullOrEmpty(parameter) || string.IsNullOrEmpty(value))
                return;

            List<string> values;
            if (!Parameters.TryGetValue(parameter.ToLower(), out values))
                values = new List<string>();

            values.Add(value);
            Parameters[parameter.ToLower()] = values;
        }

        public string GetParameterValue(string parameter)
        {
            List<string> values;
            if (!Parameters.TryGetValue(parameter.ToLower(), out values))
                return null;

            return values.FirstOrDefault();
        }

        public List<string> GetParameterValues(string parameter)
        {
            List<string> values;
            if (!Parameters.TryGetValue(parameter.ToLower(), out values))
                return new List<string>();

            return values;
        }

        public override string ToString()
        {
            string output = State.ToString();

            foreach (string key in Parameters.Keys)
                foreach (string value in Parameters[key])
                    output += string.Format(" [{0}: {1}]", key, value);

            return output;
        }
    }
}