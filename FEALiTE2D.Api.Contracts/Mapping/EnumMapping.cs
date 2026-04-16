using FEALiTE2D.Api.Contracts.Enums;
using FEALiTE2D.Elements;
using FEALiTE2D.Loads;
using FEALiTE2D.Materials;

namespace FEALiTE2D.Api.Contracts.Mapping;

internal static class EnumMapping
{
    public static LoadDirection ToDomain(this LoadDirectionDto v) => v switch
    {
        LoadDirectionDto.Local => LoadDirection.Local,
        _ => LoadDirection.Global
    };

    public static LoadCaseType ToDomain(this LoadCaseTypeDto v) => v switch
    {
        LoadCaseTypeDto.Dead => LoadCaseType.Dead,
        LoadCaseTypeDto.Live => LoadCaseType.Live,
        LoadCaseTypeDto.Wind => LoadCaseType.Wind,
        LoadCaseTypeDto.Seismic => LoadCaseType.Seismic,
        LoadCaseTypeDto.Accidental => LoadCaseType.Accidental,
        LoadCaseTypeDto.Shrinkage => LoadCaseType.Shrinkage,
        _ => LoadCaseType.SelfWeight
    };

    public static LoadCaseDuration ToDomain(this LoadCaseDurationDto v) => v switch
    {
        LoadCaseDurationDto.LongTerm => LoadCaseDuration.LongTerm,
        LoadCaseDurationDto.MediumTerm => LoadCaseDuration.MediumTerm,
        LoadCaseDurationDto.ShortTerm => LoadCaseDuration.ShortTerm,
        LoadCaseDurationDto.Instantaneous => LoadCaseDuration.Instantaneous,
        _ => LoadCaseDuration.Permanent
    };

    public static MaterialType ToDomain(this MaterialTypeDto v) => v switch
    {
        MaterialTypeDto.Steel => MaterialType.Steel,
        MaterialTypeDto.Timber => MaterialType.Timber,
        MaterialTypeDto.Aluminum => MaterialType.Aluminum,
        MaterialTypeDto.Userdefined => MaterialType.Userdefined,
        _ => MaterialType.Concrete
    };

    public static Frame2DEndRelease ToDomain(this EndReleaseDto v) => v switch
    {
        EndReleaseDto.StartRelease => Frame2DEndRelease.StartRelease,
        EndReleaseDto.EndRelease => Frame2DEndRelease.EndRelease,
        EndReleaseDto.FullRelease => Frame2DEndRelease.FullRelease,
        _ => Frame2DEndRelease.NoRelease
    };
}
