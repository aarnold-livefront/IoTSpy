import React, { useState, useEffect, useRef } from 'react';
import { useWebSocket } from '../hooks/useWebSocket';
import './NetworkDeviceSelector.css';

interface NetworkDevice {
  name: string;
  description: string;
  isOnline: boolean;
  interfaceName: string;
}

export function NetworkDeviceSelector({ onDeviceSelect }: { onDeviceSelect: (device: NetworkDevice) => void }) {
  const [devices, setDevices] = useState<NetworkDevice[]>([]);
  const [selectedDevice, setSelectedDevice] = useState<NetworkDevice | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    // Request network device enumeration from backend
    fetch('/api/packet-capture/devices', {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
      .then(res => res.json())
      .then(data => setDevices(data))
      .catch(err => console.error('Failed to load devices:', err));

    // WebSocket for real-time device status updates
    const ws = new WebSocket(`ws://${window.location.host}/packet-capture`);
    ws.onmessage = (event) => {
      try {
        const update = JSON.parse(event.data);
        if (update.type === 'DEVICE_UPDATE') {
          setDevices(prev => prev.map(d => 
            d.name === update.deviceName 
              ? { ...d, isOnline: update.isOnline }
              : d
          ));
        }
      } catch { /* ignore */ }
    };

    return () => ws.close();
  }, []);

  const handleSelect = async (device: NetworkDevice) => {
    setIsLoading(true);
    try {
      setSelectedDevice(device);
      
      // Start packet capture on selected device
      await fetch('/api/packet-capture/start', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('token')}`
        },
        body: JSON.stringify({ 
          interfaceName: device.interfaceName,
          captureFilter: '' // Can be enhanced with BPF filter later
        })
      });
      
      onDeviceSelect(device);
    } catch (err) {
      console.error('Failed to start capture:', err);
      setSelectedDevice(null);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="network-device-selector">
      <h3>Select Network Device for Capture</h3>
      
      {devices.length === 0 && !isLoading ? (
        <p className="no-devices">No network devices available</p>
      ) : (
        <ul className="device-list">
          {devices.map(device => (
            <li 
              key={device.name}
              className={`device-item ${selectedDevice?.name === device.name ? 'selected' : ''} ${!device.isOnline ? 'offline' : ''}`}
              onClick={() => device.isOnline && handleSelect(device)}
            >
              <div className="device-info">
                <span className="device-name">{device.name}</span>
                <span className="device-description">{device.description}</span>
              </div>
              {selectedDevice?.name === device.name && (
                <span className="status-indicator selected-status">Selected</span>
              )}
              {!device.isOnline && (
                <span className="status-indicator offline-status">Offline</span>
              )}
            </li>
          ))}
        </ul>
      )}

      {isLoading && (
        <div className="loading-overlay">
          <div className="spinner"></div>
          <p>Selecting device...</p>
        </div>
      )}
    </div>
  );
}
