FeatureScript 2200;

// 若 Feature Studio 提示版本不匹配，点击编辑器中的灯泡即可更新到当前工作室的版本。
import(path : "onshape/std/geometry.fs", version : "2200.0");

// ===========================================================================
// Count Teeth —— 自动识别带轮(pulley) / 链轮(sprocket) 的齿数
//
// 原理:
//   带轮/链轮本质上是“齿廓草图沿轴线拉伸”形成的棱柱体, 它的两个端面(侧平面)
//   的外边界就是完整的齿廓。本特征:
//     1. 由用户指定(或自动识别)回转轴;
//     2. 找出垂直于该轴的侧平面上的“齿廓边”(两端点轴向坐标相同 => 位于⊥轴平面);
//     3. 在所有侧平面边中选出边数最多的那一组(齿廓所在的端面);
//     4. 把该端面的边按连通关系分成若干环, 取半径最大的环 = 齿顶外环;
//     5. 沿外环顺序采样每个边的起点与中点的半径;
//     6. 以“齿根半径 + 0.7×齿高”为阈值, 统计半径序列中超过阈值的连续峰值数,
//        即齿数。(中点采样可正确处理圆弧齿顶、平顶齿等情形)
// ===========================================================================

// 点到轴线的径向距离
function radialDistance(point is Vector, axisOrigin is Vector, axisDir is Vector) returns ValueWithUnits
{
    var d = point - axisOrigin;
    return norm(d - dot(d, axisDir) * axisDir);
}

// 两个带长度单位的点是否重合
function pointsEqual(a is Vector, b is Vector) returns boolean
{
    return norm(a - b) < 1e-4 * millimeter;
}

// 取一条边的两个端点(以位置向量返回)。周期边(整圆)没有顶点, 返回空数组。
function edgeEndPoints(context is Context, edge is Query) returns array
{
    var verts = evaluateQuery(context, qAdjacent(edge, AdjacencyType.VERTEX));
    var pts = [];
    for (var v in verts)
    {
        pts = append(pts, evVertexPoint(context, { "vertex" : v }));
    }
    return pts;
}

// 把一个环上的边按共享顶点串成有序序列。
// edgeInfos 的每个元素形如 { "edge" : Query, "points" : [p0, p1] }
function orderLoopEdges(edgeInfos is array) returns array
{
    if (size(edgeInfos) == 0)
        return [];

    var remaining = edgeInfos;
    var ordered = [remaining[0]];
    var currentEnd = remaining[0].points[1];

    // 去掉第一个元素
    var rest = [];
    for (var i = 1; i < size(remaining); i += 1)
        rest = append(rest, remaining[i]);
    remaining = rest;

    while (size(remaining) > 0)
    {
        var found = false;
        for (var i = 0; i < size(remaining); i += 1)
        {
            var pts = remaining[i].points;
            if (size(pts) < 2)
                continue; // 周期边不属于齿廓外环, 跳过

            if (pointsEqual(pts[0], currentEnd))
            {
                ordered = append(ordered, remaining[i]);
                currentEnd = pts[1];
                found = true;
            }
            else if (pointsEqual(pts[1], currentEnd))
            {
                ordered = append(ordered, remaining[i]);
                currentEnd = pts[0];
                found = true;
            }

            if (found)
            {
                var newRemaining = [];
                for (var j = 0; j < size(remaining); j += 1)
                    if (j != i)
                        newRemaining = append(newRemaining, remaining[j]);
                remaining = newRemaining;
                break;
            }
        }
        if (!found)
            break; // 不连通, 停止
    }
    return ordered;
}

// 在周期性半径序列中统计超过阈值的“连续峰值”数量(即齿数)
function countPeaksAbove(samples is array, threshold is ValueWithUnits) returns number
{
    var n = size(samples);
    if (n == 0)
        return 0;
    var count = 0;
    for (var i = 0; i < n; i += 1)
    {
        var prev = samples[(i - 1 + n) % n];
        var cur = samples[i];
        if (prev < threshold && cur >= threshold)
            count += 1; // 从下方穿越阈值 => 新的一个齿
    }
    return count;
}

annotation { "Feature Type Name" : "Count Teeth", "Feature Type Description" : "识别带轮/链轮的齿数" }
export const countTeeth = defineFeature(function(context is Context, id is Id, definition is map)
    precondition
    {
        annotation { "Name" : "带轮/链轮零件", "Filter" : EntityType.BODY && BodyType.SOLID, "MaxNumberOfPicks" : 1 }
        definition.part is Query;

        annotation { "Name" : "回转轴(圆柱面或圆边)", "Filter" : QueryFilterConstraint.ALLOWS_AXIS, "MaxNumberOfPicks" : 1 }
        definition.axis is Query;

        annotation { "Name" : "用齿数重命名零件", "Default" : true }
        definition.rename is boolean;

        annotation { "Name" : "名称前缀", "Default" : "Pulley" }
        definition.namePrefix is string;
    }
    {
        // ---------- 1. 解析回转轴 --------------------------------------------
        var ax = evAxis(context, { "axis" : definition.axis });
        var axisOrigin is Vector = ax.origin;
        var axisDir is Vector = ax.direction;

        // ---------- 2. 收集齿廓边 --------------------------------------------
        // 齿廓边位于垂直于轴的平面内 => 两端点的轴向坐标相同。
        var allEdges = evaluateQuery(context, qOwnedByBody(definition.part, EntityType.EDGE));

        var profileEdgeInfos = []; // { "edge", "points", "z" }
        for (var edge in allEdges)
        {
            var pts = edgeEndPoints(context, edge);
            if (size(pts) < 2)
                continue; // 整圆(如内孔)无顶点, 稍后按环处理

            var z0 = dot(pts[0] - axisOrigin, axisDir);
            var z1 = dot(pts[1] - axisOrigin, axisDir);
            if (norm(z0 - z1) < 1e-3 * millimeter)
                profileEdgeInfos = append(profileEdgeInfos, { "edge" : edge, "points" : pts, "z" : z0 });
        }

        if (size(profileEdgeInfos) == 0)
            throw "未找到齿廓边。请确认所选零件是带轮或链轮, 且具有垂直于轴的平端面。";

        // ---------- 3. 按轴向位置聚类, 取边数最多的一组(齿廓端面) ------------
        var clusters = []; // { "z", "edges" : [...] }
        for (var info in profileEdgeInfos)
        {
            var placed = false;
            var newClusters = [];
            for (var c in clusters)
            {
                if (!placed && norm(c.z - info.z) < 1e-2 * millimeter)
                {
                    newClusters = append(newClusters, { "z" : c.z, "edges" : append(c.edges, info.edge) });
                    placed = true;
                }
                else
                {
                    newClusters = append(newClusters, c);
                }
            }
            if (!placed)
                newClusters = append(newClusters, { "z" : info.z, "edges" : [info.edge] });
            clusters = newClusters;
        }

        var bestCluster = clusters[0];
        for (var c in clusters)
        {
            if (size(c.edges) > size(bestCluster.edges))
                bestCluster = c;
        }
        var sideEdges = bestCluster.edges;

        // ---------- 4. 分环, 选半径最大的环(齿顶外环) -----------------------
        var loops = []; // 每个元素: 边查询数组
        var processed = new box([]);
        for (var edge in sideEdges)
        {
            if (isIn(edge, processed[]))
                continue;
            var loopEdges = evaluateQuery(context, qLoopEdges(edge));
            loops = append(loops, loopEdges);
            var np = [];
            for (var e in processed[])
                np = append(np, e);
            for (var e in loopEdges)
                np = append(np, e);
            processed[] = np;
        }

        var outerLoop = loops[0];
        var outerMaxR = 0 * millimeter;
        for (var loop in loops)
        {
            var maxR = 0 * millimeter;
            for (var edge in loop)
            {
                for (var p in edgeEndPoints(context, edge))
                {
                    var r = radialDistance(p, axisOrigin, axisDir);
                    if (r > maxR)
                        maxR = r;
                }
            }
            if (maxR > outerMaxR)
            {
                outerMaxR = maxR;
                outerLoop = loop;
            }
        }

        // 高亮整个齿顶外环(绿色)以便核对
        for (var e in outerLoop)
            addDebugEntities(context, e, DebugColor.GREEN);

        // ---------- 5. 有序化外环, 采样半径 ----------------------------------
        var infos = [];
        for (var edge in outerLoop)
        {
            var pts = edgeEndPoints(context, edge);
            if (size(pts) >= 2)
                infos = append(infos, { "edge" : edge, "points" : pts });
        }
        var ordered = orderLoopEdges(infos);

        if (size(ordered) < 3)
            throw "无法解析齿廓环。端面可能是光滑整圆(无齿)。";

        if (size(ordered) != size(infos))
            throw "齿廓环未能完整串接, 几何可能存在异常。";

        // 沿环顺序采样: 每条边的 [起点, 中点]
        var samples = [];
        for (var info in ordered)
        {
            var startPt = info.points[0];
            var midPt = evEdgeTangentLine(context, { "edge" : info.edge, "parameter" : 0.5 }).origin;
            samples = append(samples, radialDistance(startPt, axisOrigin, axisDir));
            samples = append(samples, radialDistance(midPt, axisOrigin, axisDir));
        }

        // ---------- 6. 计算阈值并数齿 ---------------------------------------
        var rMax = samples[0];
        var rMin = samples[0];
        for (var s in samples)
        {
            if (s > rMax) rMax = s;
            if (s < rMin) rMin = s;
        }

        if (rMax <= 0 * millimeter || (rMax - rMin) / rMax < 0.005)
        {
            println("未检测到齿 —— 外轮廓近似为光滑整圆(例如 V 带轮)。");
            return;
        }

        var tipThreshold = rMin + (rMax - rMin) * 0.7; // 顶部 30% 视为齿顶
        var teeth = countPeaksAbove(samples, tipThreshold);

        if (teeth < 2)
            throw "齿数异常(" ~ toString(teeth) ~ "), 请确认所选零件是带轮或链轮。";

        // 红色标出被判定为齿顶的采样点
        for (var k = 0; k < size(samples); k += 1)
        {
            if (samples[k] >= tipThreshold)
            {
                var idx = floor(k / 2);
                var isMid = (k % 2 == 1);
                var pos = isMid
                    ? evEdgeTangentLine(context, { "edge" : ordered[idx].edge, "parameter" : 0.5 }).origin
                    : ordered[idx].points[0];
                try silent
                {
                    debug(context, pos, DebugColor.RED);
                }
            }
        }

        // ---------- 7. 输出结果 ---------------------------------------------
        if (definition.rename)
            setProperty(context, { "entities" : definition.part, "propertyName" : PropertyType.NAME, "value" : definition.namePrefix ~ " (" ~ toString(teeth) ~ "T)" });

        println("已识别齿数: " ~ toString(teeth) ~ "  (齿顶半径 " ~ toString(rMax) ~ ", 齿根半径 " ~ toString(rMin) ~ ")");
    });
