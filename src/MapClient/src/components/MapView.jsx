import React, { useEffect, useRef, useState } from "react";
import "ol/ol.css";
import { Map, View } from "ol";
import TileLayer from "ol/layer/Tile";
import { OSM } from "ol/source";
import VectorLayer from "ol/layer/Vector";
import VectorSource from "ol/source/Vector";
import { fromLonLat, toLonLat } from "ol/proj";
import { Point } from "ol/geom";
import { Feature } from "ol";
import { Style, Icon, Text, Fill, Stroke } from "ol/style";
import Popup from "./Popup";
import { fetchCenterCoordinates, fetchHives, moveHives } from "../api/mapService";

// TEST STAGING

// TODO: Hardcoded marker icon path
const MARKER_ICON_URL = "/256x256.png";

const MapView = () => {
    const mapRef = useRef(null);
    const vectorLayerRef = useRef(null);
    const initialized = useRef(false);
    const apiUrl = useRef(null)
    const [hives, setHives] = useState([]);
    const [popup, setPopup] = useState({ visible: false, coords: null });
    const [mouseCoords, setMouseCoords] = useState({ lat: "", lon: "" });
    const popoverRef = useRef(null);

    useEffect(() => {
        const initializeMap = async () => {

            const res = await fetch('/config.json')
            const data = await res.json()

            apiUrl.current = data.API

            if (initialized.current) return;
            initialized.current = true;

            try {
                const center = await fetchCenterCoordinates(apiUrl.current);
                if (center) {
                    initMap(center.Latitude, center.Longitude);
                    await fetchAndDrawHives();
                }

                // 🔄 Auto-fetch hives every 30 seconds
                const interval = setInterval(fetchAndDrawHives, 5000);
                return () => clearInterval(interval);
            } catch (error) {
                console.error("Error initializing map:", error);
            }
        };

        initializeMap();
    }, []);

    // Initialize OpenLayers Map
    const initMap = (lat, lon) => {
        const map = new Map({
            target: "map-container",
            layers: [new TileLayer({ source: new OSM() })],
            view: new View({ center: fromLonLat([lon, lat]), zoom: 12 }),
        });

        map.on("pointermove", (event) => handleMouseMove(event, map));
        map.on("singleclick", (event) => handleMapClick(event, map));

        mapRef.current = map;
    };

    // Fetch hives and draw them on the map
    const fetchAndDrawHives = async () => {
        try {
            const data = await fetchHives(apiUrl.current);
            setHives(data);
            drawHives(data);
        } catch (error) {
            console.error("❌ Error fetching hives:", error);
        }
    };

    // Draw markers for all hives
    const drawHives = (hives) => {
        if (!mapRef.current) return;
        if (vectorLayerRef.current) mapRef.current.removeLayer(vectorLayerRef.current);

        const vectorSource = new VectorSource();
        hives.forEach((hive) => {
            const feature = new Feature({
                geometry: new Point(fromLonLat([hive.lon, hive.lat])),
            });

            feature.setId(hive.id);
            feature.setStyle(
                new Style({
                    image: new Icon({ src: MARKER_ICON_URL, scale: 0.05 }),
                    text: new Text({
                        text: hive.id,
                        fill: new Fill({ color: "#000" }),
                        stroke: new Stroke({ color: "#fff", width: 2 }),
                        offsetY: -20,
                    }),
                })
            );

            feature.set("id", hive.id);
            feature.set("lat", hive.lat);
            feature.set("lon", hive.lon);

            vectorSource.addFeature(feature);
        });

        const vectorLayer = new VectorLayer({ source: vectorSource });
        vectorLayerRef.current = vectorLayer;
        mapRef.current.addLayer(vectorLayer);

        mapRef.current.on("pointermove", (event) => handleMarkerHover(event, mapRef.current));
    };

    // Handle Mouse Move (Show live coordinates)
    const handleMouseMove = (event, map) => {
        if (!map) return;
        const coords = toLonLat(event.coordinate);
        setMouseCoords({
            lat: coords[1].toFixed(6),
            lon: coords[0].toFixed(6),
        });
    };

    // Show popover when hovering over a marker
    const handleMarkerHover = (event, map) => {
        if (!popoverRef.current) return;
        const features = map.getFeaturesAtPixel(event.pixel);
        if (features.length > 0) {
            const feature = features[0];
            popoverRef.current.innerHTML = `ID: ${feature.get("id")}<br>Lat: ${feature.get("lat")}<br>Lon: ${feature.get("lon")}`;
            popoverRef.current.style.left = `${event.pixel[0] + 10}px`;
            popoverRef.current.style.top = `${event.pixel[1] + 10}px`;
            popoverRef.current.style.display = "block";
        } else {
            popoverRef.current.style.display = "none";
        }
    };

    // Show popup when clicking an empty spot (Move Hives)
    const handleMapClick = (event, map) => {
        if (!map.getFeaturesAtPixel(event.pixel).length) {
            const coords = toLonLat(event.coordinate);
            setPopup({ visible: true, coords: { lat: coords[1].toFixed(6), lon: coords[0].toFixed(6) } });
        }
    };

    return (
        <div style={{ width: "100%", height: "100vh", display: "flex", flexDirection: "column", alignItems: "center" }}>
            <h1>Hive Map - Dev branch</h1>

            {/* Latitude & Longitude Inputs */}
            <div style={{ marginBottom: "10px", display: "flex", gap: "10px" }}>
                <label>Latitude: <input type="text" value={mouseCoords.lat} disabled /></label>
                <label>Longitude: <input type="text" value={mouseCoords.lon} disabled /></label>
            </div>

            {/* Map Container */}
            <div id="map-container" style={{ width: "80%", height: "80vh", border: "1px solid #ddd", position: "relative" }}></div>

            {/* Tooltip for Marker Hover */}
            <div ref={popoverRef} style={{
                position: "absolute",
                display: "none",
                background: "#fff",
                padding: "5px",
                borderRadius: "5px",
                border: "1px solid #000",
                pointerEvents: "none",
                zIndex: 999
            }}></div>

            {/* Move All Hives Popup (Centered Modal) */}
            <Popup 
                isVisible={popup.visible} 
                coords={popup.coords} 
                onConfirm={() => moveHives(apiUrl.current, popup.coords.lat, popup.coords.lon, hives.map(h => h.id))} 
                onCancel={() => setPopup({ visible: false })} 
            />
        </div>
    );
};

export default MapView;
