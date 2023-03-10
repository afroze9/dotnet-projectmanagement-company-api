namespace ProjectManagement.CompanyAPI.Model;

public class CompanyResponseModel
{
    public int Id { get; set; }

    required public string Name { get; set; }

    public virtual List<TagResponseModel> Tags { get; set; } = new ();
}