namespace QualityDoc.Services
{
    public static class DocumentWorkflowConstants
    {
        public static class Status
        {
            public const int Draft = 1;
            public const int InReview = 2;
            public const int Candidate = 3;
            public const int Rejected = 4;
            public const int Active = 5;
            public const int Obsolete = 6;
        }

        public static class Roles
        {
            public const string SuperAdmin = "Super Admin";
            public const string Admin = "Admin";
            public const string Reviewer = "Reviewer";
            public const string Redacter = "Redacter";
            public const string Operador = "Operador";
        }
    }
}
