FeatureScript 3044;

/**
 * 圆柱侧面环形排孔 (Circular Side Holes)
 * =====================================
 *
 * 在圆柱（管）侧面上打多圈沿圆周均匀分布的孔：
 *   1. 选择目标实体与圆柱端面（圆或圆环均可）——由端面的相邻圆边
 *      自动确定圆柱轴线（端面法向）、圆心与外径
 *   2. 每圈孔（最多 4 圈）独立设置：
 *      - 第 1 圈孔中心离所选端面的轴向距离；之后每圈离前一圈孔中心的轴向距离
 *      - 该圈孔数（沿圆周均匀分布）
 *      - 孔直径、孔深（沿径向从外表面向轴心）
 *      - 是否通孔（直接打到圆柱轴线，忽略深度）
 *   3. 孔为径向向心方向（垂直于圆柱轴线，从外表面指向轴线）
 *
 * 端面圆边解析 / evPlane / evLength 接缝过滤手法来自 pulley_roller.fs；
 * circularPattern 用法参考 sprocket_vincentz.fs；减切布尔参考
 * pulley_roller / alexkempen 的 targets+tools SUBTRACTION 写法。
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
    // 目标实体（Filter 写法参考 belt_official.fs）
    annotation { "Name" : "Target body", "Description" : "Body to cut (cylinder or tube)", "Filter" : EntityType.BODY, "MaxNumberOfPicks" : 1 }
    definition.body is Query;

    // 圆柱端面（圆或圆环均可）
    annotation { "Name" : "Cylinder end face", "Description" : "End face (disc or annulus) that defines the cylinder axis and outer radius", "Filter" : GeometryType.PLANE && EntityType.FACE, "MaxNumberOfPicks" : 1 }
    definition.endFace is Query;

    // 圈数
    annotation { "Name" : "Ring count", "Description" : "Number of hole rings (1-4)" }
    isInteger(definition.ringCount, { (unitless) : [1, 2, 4] } as IntegerBoundSpec);

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

    // 第 2-4 圈：条件显示（ringCount 足够时）
    if (definition.ringCount > 1)
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

    if (definition.ringCount > 2)
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

    if (definition.ringCount > 3)
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

    // 1. 从端面解析圆柱参考（轴线 / 圆心 / 外半径）
    const cyl = getCylinderFromFace(context, definition.endFace);

    // 2. 整理每圈规格（轴向位置 / 孔数 / 直径 / 深度 / 通孔）
    const specs = buildRingSpecs(definition);

    // 3. 逐圈切孔
    for (var i = 0; i < size(specs); i += 1)
    {
        cutRingHoles(context, id + ("ring" ~ toString(i)), definition, cyl, specs[i]);
    }

    // 4. 清理草图（opDeleteBodies + qSketchFilter，参考 neilcooke/alexkempen）
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
 * 返回 { axis, center, outerR }。
 */
function getCylinderFromFace(context is Context, face is Query)
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

    return {
                "axis" : facePlane.normal,
                "center" : outer.center,
                "outerR" : outer.radius
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

    for (var i = 0; i < definition.ringCount; i += 1)
    {
        const dist = i == 0 ? definition.ring1Distance : getRingDistance(definition, i);
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
    return ring == 1 ? definition.ring2Distance
         : ring == 2 ? definition.ring3Distance
         : definition.ring4Distance;
}

function getRingHoleCount(definition is map, ring is number)
{
    return ring == 0 ? definition.count1
         : ring == 1 ? definition.count2
         : ring == 2 ? definition.count3
         : definition.count4;
}

function getRingHoleDiameter(definition is map, ring is number)
{
    return ring == 0 ? definition.dia1
         : ring == 1 ? definition.dia2
         : ring == 2 ? definition.dia3
         : definition.dia4;
}

function getRingHoleDepth(definition is map, ring is number)
{
    return ring == 0 ? definition.depth1
         : ring == 1 ? definition.depth2
         : ring == 2 ? definition.depth3
         : definition.depth4;
}

function getRingHoleThrough(definition is map, ring is number)
{
    return ring == 0 ? definition.through1
         : ring == 1 ? definition.through2
         : ring == 2 ? definition.through3
         : definition.through4;
}

// ---------------------------------------------------------------
// 几何生成
// ---------------------------------------------------------------

/**
 * 切一圈孔：
 *   草图平面包含轴线（与 pulley_roller makeCollar 相同构造：法向 = 轴向、
 *   x = 轴向、y = 径向）。孔心在 (z, outerR)——即外表面上。
 *   沿 -y（径向向内）拉伸深度，绕轴 circularPattern，减切目标实体。
 */
function cutRingHoles(context is Context, ringId is Id, definition is map, cyl is map, spec is map)
{
    const axis = cyl.axis;
    const center = cyl.center;
    const outerR = cyl.outerR;

    // 草图平面：原点 = 圆心，法向垂直于轴（包含轴线的平面）
    // x 沿轴（孔的轴向位置），y 沿径向（孔的径向位置）
    const radialDir = perpendicularVector(axis);
    const skPlane = plane(center, radialDir, axis);

    // 孔心（草图坐标）：x = 轴向位置 z，y = 外表面半径 outerR
    const holeCenter = vector(spec.z, outerR);
    const holeR = spec.diameter / 2;

    // 深度：通孔打到轴线（= outerR），否则用户深度
    const depth = spec.through ? outerR : spec.depth;

    const sk = newSketchOnPlane(context, ringId + "sketch", {
                "sketchPlane" : skPlane
            });
    skCircle(sk, "hole", {
                "center" : holeCenter,
                "radius" : holeR
            });
    skSolve(sk);

    // 沿 -y（径向向内）拉伸
    opExtrude(context, ringId + "extrude", {
                "entities" : qSketchRegion(ringId + "sketch"),
                "direction" : -skPlane.yAxis,
                "endBound" : BoundingType.BLIND,
                "endDepth" : depth
            });

    // 绕轴线均匀阵列 count-1 个副本（opPattern + rotationAround，
    // 参考 neilcooke spur gear 的齿阵列写法）
    var transforms = [];
    for (var r = 1; r < spec.count; r += 1)
    {
        transforms = append(transforms, rotationAround(line(center, axis), r * (360 / spec.count) * degree));
    }
    opPattern(context, ringId + "pattern", {
                "entities" : qCreatedBy(ringId + "extrude", EntityType.BODY),
                "transforms" : transforms,
                "instanceNames" : []
            });

    // 减切目标实体（targets = 实体，tools = 孔阵列；参考 alexkempen cutBore）
    opBoolean(context, ringId + "cut", {
                "targets" : definition.body,
                "tools" : qUnion([
                            qCreatedBy(ringId + "extrude", EntityType.BODY),
                            qCreatedBy(ringId + "pattern", EntityType.BODY)
                        ]),
                "operationType" : BooleanOperationType.SUBTRACTION
            });
}
