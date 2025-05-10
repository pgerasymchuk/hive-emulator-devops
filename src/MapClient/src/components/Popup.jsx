import React from "react";

const Popup = ({ isVisible, coords, onConfirm, onCancel }) => {
    if (!isVisible || !coords) return null;

    // Copy coordinates to clipboard
    const copyCoordinates = async () => {
        const textToCopy = `[${coords.lat}, ${coords.lon}]`;
        try {
            await navigator.clipboard.writeText(textToCopy);
            alert("Coordinates copied to clipboard!");
        } catch (error) {
            console.error("Error copying coordinates:", error);
        }
    };

    return (
        <div style={{
            position: "fixed",
            top: "0",
            left: "0",
            width: "100vw",
            height: "100vh",
            backgroundColor: "rgba(0, 0, 0, 0.5)", // Semi-transparent background
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 1000
        }}>
            <div style={{
                backgroundColor: "white",
                padding: "20px",
                boxShadow: "0px 0px 15px rgba(0,0,0,0.3)",
                borderRadius: "8px",
                textAlign: "center",
                minWidth: "300px"
            }}>
                <h3>Move all hives to:</h3>
                <p>Lat: {coords.lat} | Lon: {coords.lon}</p>

                {/* Copy Coordinates Button */}
                <button onClick={copyCoordinates} style={{ marginBottom: "10px", display: "block", width: "100%" }}>Copy Coordinates</button>

                {/* Move Hives & Cancel Buttons */}
                <button onClick={() => onConfirm(coords)} style={{ marginRight: "10px" }}>Move Hives</button>
                <button onClick={onCancel}>Cancel</button>
            </div>
        </div>
    );
};

export default Popup;
