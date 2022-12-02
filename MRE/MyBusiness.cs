namespace MRE
{
    public class MyBusiness
    {
        public string GetDisplayValue(bool? val)
        {
            if (val == null)
                return "maybe";

            if (val.Value)
                return "yes";

            return "no";
        }
    }
}