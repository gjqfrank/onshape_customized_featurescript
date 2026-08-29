FeatureScript 3044;

/**
 * 圆柱侧面环形排孔 (Circular Side Holes)
 * =====================================
 *
 * 在圆柱（管）侧面上打多圈沿圆周均匀分布的径向孔：
 *   1. 选择目标实体与圆柱端面（圆或圆环均可）——由端面的相邻圆边
 *      自动确定圆柱轴线（端面法向）、圆心与外径
 *   2. 每圈孔（最多 4 圈）独立设置：
 *      - 第 1 圈孔中心离所选端面的轴向距离；之后每圈离前一圈孔中心的轴向距离
 *      - 该圈孔数（沿圆周均匀分布）
 *      - 孔直径、孔深（沿径向从外表面向轴心）
 *      - 是否通孔（直接打到圆柱轴线，忽略深度）
 *   3. 孔为径向向心方向（垂直于圆柱轴线，从外表面指向轴线）
 *
 * 已规避的 FeatureScript 陷阱：
 *   - skCircle center 必须是带长度单位的 2D 点：vector(0, 0) * millimeter
 *   - Vector 不支持一元负号，用 * -1
 *   - Plane 的 y 轴用 ->yAxis()；直接存的是 origin/normal/x
 *   - evCurveDefinition 圆边的圆心在 coordSystem.origin（无 center 字段）
 *   - 拉伸方向必须垂直于草图平面（沿 ±normal），不能沿面内方向
 *   - opPattern 空 transforms 会报错，孔数 = 1 时跳过阵列
 *   - 端面法向可能背离实体，需探测实体方向并翻转轴线
 */

import(path : "onshape/std/common.fs", version : "3044.0");

// ---------------------------------------------------------------
// 输入范围
// ---------------------------------------------------------------

const HOLE_DIA_BOUNDS =
{
            (millimeter) : [0.1, 3, 50],
            (inch) : 0.125
        } as LengthBoundSpec;

const HOLE_DEPTH_BOUNDS =
{
            (millimeter) : [0.01, 3, 200],
            (inch) : 0.125
        } as LengthBoundSpec;

const RING_DIST_BOUNDS =
{
            (millimeter) : [0, 5, 500],
            (inch) : 0.2
        } as LengthBoundSpec;

const HOLE_COUNT_BOUNDS =
{
            (unitless) : [1, 6, 48]
        } as IntegerBoundSpec;

// ---------------------------------------------------------------
// 主特征
// ---------------------------------------------------------------

annotation {
        "Feature Type Name" : "Circular side holes",
        "Feature Type Description" : "Cut rings of evenly distributed radial holes on a cylinder/tube side: pick a body and an end face (disc or annulus); each ring has its own hole count, diameter, depth, and through-to-axis option; ring spacing is axial."
    }
export const circularSideHoles = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
    // 目标实体
    annotation { "Name" : "Target body", "Description" : "Body to cut (cylinder or tube)", "Filter" : EntityType.BODY, "MaxNumberOfPicks" : 1 }
    definition.body is Query;

    // 圆柱端面（圆或圆环均可）
    annotation { "Name" : "Cylinder end face", "Description" : "End face (disc or annulus) that defines the cylinder axis and outer radius", "Filter" : GeometryType.PLANE && EntityType.FACE, "MaxNumberOfPicks" : 1 }
    definition.endFace is Query;

    // 圈数（布尔级联：Add ring 2/3/4）
    annotation { "Name" : "Add ring 2", "Description" : "Add a second ring of holes" }
    definition.addRing2 is boolean;

    annotation { "Name" : "Add ring 3", "Description" : "Add a third ring of holes" }
    definition.addRing3 is boolean;

    annotation { "Name" : "Add ring 4", "Description" : "Add a fourth ring of holes" }
    definition.addRing4 is boolean;

    // 第 1 圈
    annotation { "Name" : "Ring 1 distance from end face", "Description" : "Axial distance of ring 1 hole centers from the end face" }
    isLength(definition.ring1Distance, RING_DIST_BOUNDS);

    annotation { "Name" : "Ring 1 hole count", "Description" : "Holes in ring 1 (evenly distributed)" }
    isInteger(definition.count1, HOLE_COUNT_BOUNDS);

    annotation { "Name" : "Ring 1 hole diameter" }
    isLength(definition.dia1, HOLE_DIA_BOUNDS);

    annotation { "Name" : "Ring 1 through to axis", "Description" : "Cut through to the cylinder axis (depth ignored)" }
    definition.through1 is boolean;

    if (!definition.through1)
    {
        annotation { "Name" : "Ring 1 hole depth", "Description" : "Hole depth (radial, from outer surface toward axis)" }
        isLength(definition.depth1, HOLE_DEPTH_BOUNDS);
    }

    // 第 2-4 圈：条件显示
    if (definition.addRing2)
    {
        annotation { "Name" : "Ring 2 distance from ring 1", "Description" : "Axial distance of ring 2 hole centers from ring 1" }
        isLength(definition.ring2Distance, RING_DIST_BOUNDS);

        annotation { "Name" : "Ring 2 hole count" }
        isInteger(definition.count2, HOLE_COUNT_BOUNDS);

        annotation { "Name" : "Ring 2 hole diameter" }
        isLength(definition.dia2, HOLE_DIA_BOUNDS);

        annotation { "Name" : "Ring 2 through to axis" }
        definition.through2 is boolean;

        if (!definition.through2)
        {
            annotation { "Name" : "Ring 2 hole depth" }
            isLength(definition.depth2, HOLE_DEPTH_BOUNDS);
        }
    }

    if (definition.addRing3)
    {
        annotation { "Name" : "Ring 3 distance from ring 2" }
        isLength(definition.ring3Distance, RING_DIST_BOUNDS);

        annotation { "Name" : "Ring 3 hole count" }
        isInteger(definition.count3, HOLE_COUNT_BOUNDS);

        annotation { "Name" : "Ring 3 hole diameter" }
        isLength(definition.dia3, HOLE_DIA_BOUNDS);

        annotation { "Name" : "Ring 3 through to axis" }
        definition.through3 is boolean;

        if (!definition.through3)
        {
            annotation { "Name" : "Ring 3 hole depth" }
            isLength(definition.depth3, HOLE_DEPTH_BOUNDS);
        }
    }

    if (definition.addRing4)
    {
        annotation { "Name" : "Ring 4 distance from ring 3" }
        isLength(definition.ring4Distance, RING_DIST_BOUNDS);

        annotation { "Name" : "Ring 4 hole count" }
        isInteger(definition.count4, HOLE_COUNT_BOUNDS);

        annotation { "Name" : "Ring 4 hole diameter" }
        isLength(definition.dia4, HOLE_DIA_BOUNDS);

        annotation { "Name" : "Ring 4 through to axis" }
        definition.through4 is boolean;

        if (!definition.through4)
        {
            annotation { "Name" : "Ring 4 hole depth" }
            isLength(definition.depth4, HOLE_DEPTH_BOUNDS);
        }
    }
    }

    {
    // ---------------- 运行逻辑 ----------------

    // 1. 从端面解析圆柱参考（指向实体内部的轴线 / 圆心 / 外半径）
    const cyl = getCylinderFromFace(context, definition.endFace, definition.body);

    // 2. 整理每圈规格（轴向位置 / 孔数 / 直径 / 深度 / 通孔）
    const specs = buildRingSpecs(definition);

    // 3. 逐圈切孔
    for (var i = 0; i < size(specs); i += 1)
    {
        cutRingHoles(context, id + ("ring" ~ toString(i)), definition, cyl, specs[i]);
    }

    // 4. 清理草图
    opDeleteBodies(context, id + "deleteSketches", {
                "entities" : qCreatedBy(id, EntityType.BODY)->qSketchFilter(SketchObject.YES)
            });
});

// ---------------------------------------------------------------
// 几何解析
// ---------------------------------------------------------------

/**
 * 从所选端面解析圆柱参考。
 * 端面可以是圆（实心）或圆环（管）：取相邻圆边中半径最大者为外圆。
 * 返回 { axis, center, outerR }，其中 axis 已保证指向实体内部。
 */
function getCylinderFromFace(context is Context, face is Query, body is Query)
{
    // 面必须是平面
    const facePlane = try(evPlane(context, { "face" : face }));
    if (facePlane == undefined)
    {
        throw regenError("所选面必须是平面（圆柱端面）");
    }

    // 相邻边（EDGE 邻接），过滤零长度接缝边，只留圆边
    const edges = evaluateQuery(context, qAdjacent(face, AdjacencyType.EDGE));
    var circleEdges = [];
    for (var e = 0; e < size(edges); e += 1)
    {
        try silent
        {
            if (evLength(context, { "entities" : edges[e] }) < 1e-6 * meter)
            {
                continue;
            }
            const cd = evCurveDefinition(context, { "edge" : edges[e] });
            if (cd.curveType == CurveType.CIRCLE)
            {
                circleEdges = append(circleEdges, cd);
            }
        }
    }

    if (size(circleEdges) == 0)
    {
        throw regenError("端面没有圆形边界边，无法确定圆柱轴线与半径");
    }

    var outer = circleEdges[0];
    for (var c = 1; c < size(circleEdges); c += 1)
    {
        if (circleEdges[c].radius > outer.radius)
        {
            outer = circleEdges[c];
        }
    }

    // 圆边定义的圆心在 coordSystem.origin（无 center 字段）
    const center = outer.coordSystem.origin;
    const outerR = outer.radius;

    // 端面法向可能指向实体外：用贴外壁的探测点判断实体在法向哪一侧，
    // 保证 axis 从端面指向实体内部（孔的轴向位置沿 axis 正向累计）
    var axis = facePlane.normal;
    const probeDir = perpendicularVector(axis);
    const probePoint = center + axis * 0.01 * millimeter + probeDir * (outerR - 0.01 * millimeter);
    const probePointBack = center + axis * -0.01 * millimeter + probeDir * (outerR - 0.01 * millimeter);
    const insideFront = size(evaluateQuery(context, qContainsPoint(body, probePoint))) > 0;
    const insideBack = size(evaluateQuery(context, qContainsPoint(body, probePointBack))) > 0;

    if (!insideFront && insideBack)
    {
        axis = axis * -1;
    }
    else if (!insideFront && !insideBack)
    {
        throw regenError("无法确定实体在端面的哪一侧，请确认所选实体与端面属于同一圆柱");
    }

    return {
                "axis" : axis,
                "center" : center,
                "outerR" : outerR
            };
}

// ---------------------------------------------------------------
// 圈规格构建
// ---------------------------------------------------------------

/**
 * 每圈规格数组。轴向位置：第 1 圈 = ring1Distance；第 k 圈 = 前一圈 + ringkDistance。
 */
function buildRingSpecs(definition is map) returns array
{
    var specs = [];
    var z = 0 * millimeter;

    const ringCount = 1 + (definition.addRing2 ? 1 : 0) + (definition.addRing3 ? 1 : 0) + (definition.addRing4 ? 1 : 0);
    for (var i = 0; i < ringCount; i += 1)
    {
        var dist;
        if (i == 0)
        {
            dist = definition.ring1Distance;
        }
        else
        {
            dist = getRingDistance(definition, i);
        }
        z = z + dist;

        specs = append(specs, {
            "count" : getRingHoleCount(definition, i),
            "diameter" : getRingHoleDiameter(definition, i),
            "depth" : getRingHoleDepth(definition, i),
            "through" : getRingHoleThrough(definition, i),
            "z" : z
        });
    }

    return specs;
}

// 每圈参数取值辅助（ring 从 0 计）
function getRingDistance(definition is map, ring is number)
{
    if (ring == 1)
    {
        return definition.ring2Distance;
    }
    else if (ring == 2)
    {
        return definition.ring3Distance;
    }
    else
    {
        return definition.ring4Distance;
    }
}

function getRingHoleCount(definition is map, ring is number)
{
    if (ring == 0)
    {
        return definition.count1;
    }
    else if (ring == 1)
    {
        return definition.count2;
    }
    else if (ring == 2)
    {
        return definition.count3;
    }
    else
    {
        return definition.count4;
    }
}

function getRingHoleDiameter(definition is map, ring is number)
{
    if (ring == 0)
    {
        return definition.dia1;
    }
    else if (ring == 1)
    {
        return definition.dia2;
    }
    else if (ring == 2)
    {
        return definition.dia3;
    }
    else
    {
        return definition.dia4;
    }
}

function getRingHoleDepth(definition is map, ring is number)
{
    if (ring == 0)
    {
        return definition.depth1;
    }
    else if (ring == 1)
    {
        return definition.depth2;
    }
    else if (ring == 2)
    {
        return definition.depth3;
    }
    else
    {
        return definition.depth4;
    }
}

function getRingHoleThrough(definition is map, ring is number)
{
    if (ring == 0)
    {
        return definition.through1;
    }
    else if (ring == 1)
    {
        return definition.through2;
    }
    else if (ring == 2)
    {
        return definition.through3;
    }
    else
    {
        return definition.through4;
    }
}

// ---------------------------------------------------------------
// 几何生成
// ---------------------------------------------------------------

/**
 * 切一圈孔：
 *   孔心在外表面（轴心 + 轴向 z + 径向 outerR）。草图平面法向 = 径向
 *   （即孔轴方向）、x = 轴向；圆画在草图原点（带长度单位的 2D 点），
 *   沿 -normal（径向向心）拉伸深度，绕轴 circularPattern，减切目标实体。
 */
function cutRingHoles(context is Context, ringId is Id, definition is map, cyl is map, spec is map)
{
    const axis = cyl.axis;
    const center = cyl.center;
    const outerR = cyl.outerR;

    const holeR = spec.diameter / 2;
    if (holeR >= outerR)
    {
        throw regenError("孔直径不能大于等于圆柱外径");
    }

    // 孔心（3D）：轴心 + 轴向偏移 spec.z + 径向偏移 outerR（外表面上）
    const radialDir = perpendicularVector(axis);
    const holeCenter = center + axis * spec.z + radialDir * outerR;

    // 深度：通孔打到轴线（= outerR），否则用户深度
    var depth;
    if (spec.through)
    {
        depth = outerR;
    }
    else
    {
        depth = spec.depth;
    }

    // 草图平面：法向 = 径向（即孔轴方向），原点 = 孔心，x 沿轴向。
    // 孔轴垂直于草图平面，沿 -normal（径向向心）拉伸。
    const skPlane = plane(holeCenter, radialDir, axis);

    const sk = newSketchOnPlane(context, ringId + "sketch", {
                "sketchPlane" : skPlane
            });
    skCircle(sk, "hole", {
                "center" : vector(0, 0) * millimeter,
                "radius" : holeR
            });
    skSolve(sk);

    // 沿 -normal（径向向心）拉伸
    opExtrude(context, ringId + "extrude", {
                "entities" : qSketchRegion(ringId + "sketch"),
                "direction" : skPlane.normal * -1,
                "endBound" : BoundingType.BLIND,
                "endDepth" : depth
            });

    // 减切工具：先阵列（孔数 > 1 时），把孔圆柱与阵列副本并集后统一减切
    var tools = [qCreatedBy(ringId + "extrude", EntityType.BODY)];

    if (spec.count > 1)
    {
        // 绕轴线均匀阵列 count-1 个副本（instanceNames 长度须与 transforms 一致）
        var transforms = [];
        var instanceNames = [];
        for (var r = 1; r < spec.count; r += 1)
        {
            transforms = append(transforms, rotationAround(line(center, axis), r * (360 / spec.count) * degree));
            instanceNames = append(instanceNames, "hole" ~ r);
        }
        opPattern(context, ringId + "pattern", {
                    "entities" : qCreatedBy(ringId + "extrude", EntityType.BODY),
                    "transforms" : transforms,
                    "instanceNames" : instanceNames
                });
        tools = append(tools, qCreatedBy(ringId + "pattern", EntityType.BODY));
    }

    // 减切目标实体（targets = 实体，tools = 孔圆柱 + 阵列副本）
    opBoolean(context, ringId + "cut", {
                "targets" : definition.body,
                "tools" : qUnion(tools),
                "operationType" : BooleanOperationType.SUBTRACTION
            });
}
