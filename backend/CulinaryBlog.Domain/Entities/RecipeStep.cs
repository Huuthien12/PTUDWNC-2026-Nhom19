namespace CulinaryBlog.Domain.Entities;

public class RecipeStep
{
    public Guid Id { get; set; }

    public Guid RecipeId { get; set; }

    public int StepNumber { get; set; }

    public string Instruction { get; set; } = string.Empty;
}