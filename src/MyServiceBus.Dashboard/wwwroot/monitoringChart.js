import * as d3 from "https://cdn.jsdelivr.net/npm/d3@7/+esm";

const colors = ["#55d6a9", "#69a8ff", "#f2bd5c", "#d68cff", "#ff837a", "#66d7e8"];
const flowMaps = new WeakMap();

export function renderThroughputChart(element, rawPoints) {
    const points = rawPoints.map(point => ({
        application: point.applicationName,
        timestamp: new Date(point.timestampUtc),
        value: point.consumedPerSecond
    }));
    const series = d3.groups(points, point => point.application)
        .sort(([left], [right]) => d3.ascending(left, right));

    const root = d3.select(element);
    root.selectAll("*").remove();
    if (series.length === 0) {
        root.append("p").attr("class", "panel-empty").text("Waiting for throughput samples.");
        return;
    }

    const legend = root.append("div").attr("class", "chart-legend");
    series.forEach(([name, values], index) => {
        const item = legend.append("span");
        item.append("i").style("background", colors[index % colors.length]);
        item.append("span").text(name);
        item.append("strong").text(`${d3.format(".2f")(values.at(-1).value)} / sec`);
    });

    const width = 800;
    const height = 230;
    const margin = { top: 18, right: 16, bottom: 34, left: 50 };
    const timestamps = points.map(point => point.timestamp);
    const x = d3.scaleUtc()
        .domain(d3.extent(timestamps))
        .range([margin.left, width - margin.right]);
    const y = d3.scaleLinear()
        .domain([0, Math.max(1, d3.max(points, point => point.value))])
        .nice()
        .range([height - margin.bottom, margin.top]);
    const line = d3.line()
        .x(point => x(point.timestamp))
        .y(point => y(point.value))
        .curve(d3.curveMonotoneX);

    const svg = root.append("svg")
        .attr("viewBox", `0 0 ${width} ${height}`)
        .attr("role", "img")
        .attr("aria-label", "Consumed messages per second by application");

    svg.append("g")
        .attr("class", "chart-grid")
        .attr("transform", `translate(${margin.left},0)`)
        .call(d3.axisLeft(y).ticks(4).tickSize(-(width - margin.left - margin.right)).tickFormat(""));
    svg.append("g")
        .attr("class", "chart-axis")
        .attr("transform", `translate(0,${height - margin.bottom})`)
        .call(d3.axisBottom(x).ticks(5).tickFormat(d3.utcFormat("%H:%M:%S")));
    svg.append("g")
        .attr("class", "chart-axis")
        .attr("transform", `translate(${margin.left},0)`)
        .call(d3.axisLeft(y).ticks(4));

    series.forEach(([, values], index) => {
        svg.append("path")
            .datum(values)
            .attr("class", "chart-line")
            .attr("stroke", colors[index % colors.length])
            .attr("d", line);
    });
}

export function initializeFlowMap(element) {
    disposeFlowMap(element);
    flowMaps.set(element, new FlowMap(element));
}

export function updateFlowMap(element, graph) {
    flowMaps.get(element)?.update(graph);
}

export function disposeFlowMap(element) {
    const map = flowMaps.get(element);
    if (map) {
        map.dispose();
        flowMaps.delete(element);
    }
}

class FlowMap {
    constructor(element) {
        this.element = element;
        this.nodes = [];
        this.links = [];
        this.selectedNode = null;
        this.svg = d3.select(element).select(".flow-map-canvas");
        this.stage = this.svg.append("g").attr("class", "flow-map-stage");
        this.linkGroup = this.stage.append("g").attr("class", "flow-map-links");
        this.labelGroup = this.stage.append("g").attr("class", "flow-map-labels");
        this.nodeGroup = this.stage.append("g").attr("class", "flow-map-nodes");

        const definitions = this.svg.append("defs");
        definitions.append("marker")
            .attr("id", "flow-arrow")
            .attr("viewBox", "0 -5 10 10")
            .attr("refX", 10)
            .attr("refY", 0)
            .attr("markerWidth", 7)
            .attr("markerHeight", 7)
            .attr("markerUnits", "userSpaceOnUse")
            .attr("orient", "auto")
            .append("path")
            .attr("d", "M0,-5L10,0L0,5")
            .attr("class", "flow-map-arrow");

        this.zoom = d3.zoom()
            .scaleExtent([0.35, 3.5])
            .on("zoom", event => this.stage.attr("transform", event.transform));
        this.svg.call(this.zoom);
        this.linkForce = d3.forceLink().id(node => node.id).distance(275).strength(0.5);
        this.simulation = d3.forceSimulation()
            .force("link", this.linkForce)
            .force("charge", d3.forceManyBody().strength(-780))
            .force("collide", d3.forceCollide(125).iterations(6))
            .force("x", d3.forceX().strength(0.09))
            .force("y", d3.forceY().strength(0.13))
            .on("tick", () => this.onTick());
        this.drag = d3.drag()
            .on("start", event => {
                if (!event.active) this.simulation.alphaTarget(0.18).restart();
                event.subject.fx = event.subject.x;
                event.subject.fy = event.subject.y;
            })
            .on("drag", event => {
                event.subject.fx = event.x;
                event.subject.fy = event.y;
            })
            .on("end", event => {
                if (!event.active) this.simulation.alphaTarget(0);
                event.subject.fx = null;
                event.subject.fy = null;
            });

        this.registerControls();
        this.resizeObserver = new ResizeObserver(() => this.resize());
        this.resizeObserver.observe(element);
        this.resize();
    }

    registerControls() {
        this.element.querySelector('[data-flow-map-action="zoom-in"]')
            ?.addEventListener("click", this.zoomIn = () => this.svg.transition().duration(160).call(this.zoom.scaleBy, 1.35));
        this.element.querySelector('[data-flow-map-action="zoom-out"]')
            ?.addEventListener("click", this.zoomOut = () => this.svg.transition().duration(160).call(this.zoom.scaleBy, 1 / 1.35));
        this.element.querySelector('[data-flow-map-action="reset"]')
            ?.addEventListener("click", this.reset = () => this.svg.transition().duration(180).call(this.zoom.transform, d3.zoomIdentity));
    }

    resize() {
        const width = Math.max(this.element.clientWidth, 320);
        const height = Math.max(this.element.clientHeight, 420);
        this.svg.attr("viewBox", `${-width / 2} ${-height / 2} ${width} ${height}`);
    }

    update(graph) {
        const rawNodes = graph?.nodes || [];
        const rawLinks = graph?.links || [];
        const previousNodes = new Map(this.nodes.map(node => [node.id, node]));
        const nextNodeIds = new Set(rawNodes.map(node => node.id));
        const groupedLinks = d3.rollups(
            rawLinks.filter(link => nextNodeIds.has(link.source) && nextNodeIds.has(link.target)),
            links => ({
                count: d3.sum(links, link => link.count),
                messageTypes: new Set(links.map(link => link.messageType).filter(Boolean)).size
            }),
            link => link.source,
            link => link.target)
            .flatMap(([source, targets]) => targets.map(([target, summary]) => ({
                id: `${source}->${target}`,
                source,
                target,
                ...summary
            })));
        const structureChanged = this.hasStructureChanged(rawNodes, groupedLinks);

        this.nodes = rawNodes.map(node => ({ ...previousNodes.get(node.id), ...node }));
        this.links = groupedLinks;
        this.windowSeconds = Math.max(1, graph?.windowSeconds || 300);
        this.renderLinks();
        this.renderNodes();
        this.simulation.nodes(this.nodes);
        this.linkForce.links(this.links);

        if (structureChanged) {
            this.simulation.stop().alpha(1);
            for (let index = 0; index < 180; index++) this.simulation.tick();
        }
        this.simulation.alpha(structureChanged ? 0.55 : 0.15).restart();
        this.updateHighlights();
    }

    hasStructureChanged(nodes, links) {
        if (nodes.length !== this.nodes.length || links.length !== this.links.length) return true;
        const nodeIds = new Set(this.nodes.map(node => node.id));
        const linkIds = new Set(this.links.map(link => link.id));
        return nodes.some(node => !nodeIds.has(node.id)) || links.some(link => !linkIds.has(link.id));
    }

    renderLinks() {
        const maximumCount = Math.max(1, d3.max(this.links, link => link.count) ?? 1);
        const linkWidth = d3.scaleSqrt().domain([0, maximumCount]).range([1.5, 7]);
        this.linkElements = this.linkGroup.selectAll("path")
            .data(this.links, link => link.id)
            .join(
                enter => enter.append("path").attr("opacity", 0)
                    .call(selection => selection.transition().duration(140).attr("opacity", 1)),
                update => update,
                exit => exit.transition().duration(140).attr("opacity", 0).remove())
            .attr("class", "flow-map-link")
            .attr("marker-end", "url(#flow-arrow)")
            .style("stroke-width", link => `${linkWidth(link.count)}px`);

        this.labelElements = this.labelGroup.selectAll("g")
            .data(this.links, link => link.id)
            .join(
                enter => {
                    const label = enter.append("g").attr("class", "flow-map-label");
                    label.append("rect").attr("x", -43).attr("y", -13).attr("width", 86).attr("height", 26).attr("rx", 13);
                    label.append("text").attr("text-anchor", "middle").attr("dominant-baseline", "central");
                    label.append("title");
                    return label;
                },
                update => update,
                exit => exit.remove());
        this.labelElements.select("text").text(link => `${formatFlowRate(link.count / this.windowSeconds)}/s`);
        this.labelElements.select("title").text(link =>
            `${link.count.toLocaleString()} observations · ${link.messageTypes} message type${link.messageTypes === 1 ? "" : "s"}`);
    }

    renderNodes() {
        this.nodeElements = this.nodeGroup.selectAll("g")
            .data(this.nodes, node => node.id)
            .join(
                enter => {
                    const node = enter.append("g")
                        .attr("class", "flow-map-node")
                        .attr("opacity", 0)
                        .call(this.drag)
                        .on("click", (_event, value) => {
                            this.selectedNode = this.selectedNode?.id === value.id ? null : value;
                            this.updateHighlights();
                        })
                        .on("mouseover", (_event, value) => {
                            this.hoveredNode = value;
                            this.updateHighlights();
                        })
                        .on("mouseout", () => {
                            this.hoveredNode = null;
                            this.updateHighlights();
                        });
                    node.append("rect").attr("x", -96).attr("y", -38).attr("width", 192).attr("height", 76).attr("rx", 13);
                    node.append("circle").attr("cx", -72).attr("r", 6);
                    node.append("text").attr("x", -56).attr("y", -5).attr("class", "flow-map-name");
                    node.append("text").attr("x", -56).attr("y", 17).attr("class", "flow-map-meta");
                    node.append("title");
                    node.transition().duration(140).attr("opacity", 1);
                    return node;
                },
                update => update,
                exit => exit.transition().duration(140).attr("opacity", 0).remove());
        this.nodeElements.select("circle").attr("class", node => node.onlineInstances > 0 ? "online" : "offline");
        this.nodeElements.select(".flow-map-name").text(node => trimText(node.label, 23));
        this.nodeElements.select(".flow-map-meta").text(node => `${node.onlineInstances} / ${node.totalInstances} replicas online`);
        this.nodeElements.select("title").text(node => `${node.label}\n${node.onlineInstances} of ${node.totalInstances} replicas online`);
    }

    updateHighlights() {
        const activeNode = this.hoveredNode || this.selectedNode;
        const neighbors = activeNode ? new Set(this.getNeighborIds(activeNode.id)) : null;
        this.nodeElements
            ?.classed("selected", node => this.selectedNode?.id === node.id)
            .classed("related", node => neighbors?.has(node.id) === true)
            .classed("dimmed", node => neighbors !== null && !neighbors.has(node.id));
        this.linkElements?.classed("dimmed", link => activeNode && !this.isNeighborLink(activeNode.id, link));
        this.labelElements?.classed("dimmed", link => activeNode && !this.isNeighborLink(activeNode.id, link));
    }

    getNeighborIds(nodeId) {
        const neighbors = [nodeId];
        for (const link of this.links) {
            const source = getNodeId(link.source);
            const target = getNodeId(link.target);
            if (source === nodeId) neighbors.push(target);
            if (target === nodeId) neighbors.push(source);
        }
        return neighbors;
    }

    isNeighborLink(nodeId, link) {
        return getNodeId(link.source) === nodeId || getNodeId(link.target) === nodeId;
    }

    onTick() {
        this.nodeElements?.attr("transform", node => `translate(${node.x},${node.y})`);
        this.linkElements?.attr("d", edgePath);
        this.labelElements?.attr("transform", link => {
            const midpoint = edgeMidpoint(link);
            return `translate(${midpoint.x},${midpoint.y})`;
        });
    }

    dispose() {
        this.simulation.stop();
        this.resizeObserver?.disconnect();
        this.element.querySelector('[data-flow-map-action="zoom-in"]')?.removeEventListener("click", this.zoomIn);
        this.element.querySelector('[data-flow-map-action="zoom-out"]')?.removeEventListener("click", this.zoomOut);
        this.element.querySelector('[data-flow-map-action="reset"]')?.removeEventListener("click", this.reset);
        this.svg.on(".zoom", null);
        this.svg.selectAll("*").remove();
    }
}

function edgePath(edge) {
    if (getNodeId(edge.source) === getNodeId(edge.target)) {
        return `M${edge.source.x + 62},${edge.source.y - 28} C${edge.source.x + 150},${edge.source.y - 130} ${edge.source.x - 150},${edge.source.y - 130} ${edge.source.x - 62},${edge.source.y - 28}`;
    }
    const dx = edge.target.x - edge.source.x;
    const dy = edge.target.y - edge.source.y;
    const distance = Math.max(1, Math.hypot(dx, dy));
    const startX = edge.source.x + dx / distance * 98;
    const startY = edge.source.y + dy / distance * 98;
    const endX = edge.target.x - dx / distance * 108;
    const endY = edge.target.y - dy / distance * 108;
    const bend = getNodeId(edge.source) < getNodeId(edge.target) ? 0.12 : -0.12;
    return `M${startX},${startY} Q${(startX + endX) / 2 + dy * bend},${(startY + endY) / 2 - dx * bend} ${endX},${endY}`;
}

function edgeMidpoint(edge) {
    if (getNodeId(edge.source) === getNodeId(edge.target)) return { x: edge.source.x, y: edge.source.y - 92 };
    return { x: (edge.source.x + edge.target.x) / 2, y: (edge.source.y + edge.target.y) / 2 };
}

function getNodeId(value) {
    return typeof value === "string" ? value : value.id;
}

function trimText(value, maximumLength) {
    const text = String(value || "");
    return text.length > maximumLength ? `${text.slice(0, maximumLength - 1)}…` : text;
}

function formatFlowRate(value) {
    if (value >= 1) return d3.format(".1f")(value);
    if (value >= 0.01) return d3.format(".2f")(value);
    return d3.format(".3f")(value);
}
