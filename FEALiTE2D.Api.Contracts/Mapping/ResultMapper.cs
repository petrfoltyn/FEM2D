using System.Collections.Generic;
using FEALiTE2D.Api.Contracts.Responses;
using FEALiTE2D.Elements;
using FEALiTE2D.Loads;
using FEALiTE2D.Meshing;
using FEALiTE2D.Structure;

namespace FEALiTE2D.Api.Contracts.Mapping;

public sealed class ResultMapper
{
    public CaseResultDto Map(FEALiTE2D.Structure.Structure structure, LoadCase loadCase)
    {
        var result = new CaseResultDto();
        var post = structure.Results;

        foreach (var node in structure.Nodes)
        {
            result.NodeDisplacements[node.Label] = ToDto(post.GetNodeGlobalDisplacement(node, loadCase));

            if (!node.IsFree)
            {
                var reaction = post.GetSupportReaction(node, loadCase);
                if (reaction is not null)
                    result.SupportReactions[node.Label] = ToDto(reaction);
            }
        }

        foreach (var element in structure.Elements)
        {
            var segments = post.GetElementInternalForces(element, loadCase);
            result.ElementForces[element.Label] = ToSegmentList(segments);
        }

        return result;
    }

    public CaseResultDto Map(FEALiTE2D.Structure.Structure structure, LoadCombination combination)
    {
        var result = new CaseResultDto();
        var post = structure.Results;

        foreach (var node in structure.Nodes)
        {
            var displacement = new Displacement();
            foreach (var (lc, factor) in combination)
                displacement += factor * post.GetNodeGlobalDisplacement(node, lc);

            result.NodeDisplacements[node.Label] = ToDto(displacement);

            if (!node.IsFree)
            {
                var reaction = post.GetSupportReaction(node, combination);
                if (reaction is not null)
                    result.SupportReactions[node.Label] = ToDto(reaction);
            }
        }

        foreach (var element in structure.Elements)
        {
            var segments = post.GetElementInternalForces(element, combination);
            result.ElementForces[element.Label] = ToSegmentList(segments);
        }

        return result;
    }

    private static List<SegmentDto> ToSegmentList(IList<LinearMeshSegment> segments)
    {
        var list = new List<SegmentDto>(segments.Count);
        foreach (var s in segments)
        {
            list.Add(new SegmentDto
            {
                X1 = s.x1,
                X2 = s.x2,
                StartForce = ToDto(s.Internalforces1),
                EndForce = ToDto(s.Internalforces2),
                StartDisplacement = ToDto(s.Displacement1),
                EndDisplacement = ToDto(s.Displacement2)
            });
        }
        return list;
    }

    private static DisplacementDto ToDto(Displacement d) => new()
    {
        Ux = d.Ux,
        Uy = d.Uy,
        Rz = d.Rz
    };

    private static ForceDto ToDto(Force f) => new()
    {
        Fx = f.Fx,
        Fy = f.Fy,
        Mz = f.Mz
    };
}
