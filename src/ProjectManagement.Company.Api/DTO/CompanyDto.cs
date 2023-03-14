﻿namespace ProjectManagement.CompanyAPI.DTO;

public class CompanyDto
{
    public int Id { get; set; }

    required public string Name { get; set; }

    public List<TagDto> Tags { get; set; } = new ();
}