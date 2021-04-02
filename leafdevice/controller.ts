import SmartMeterDevice, { DeviceEvents } from './device';
import crypto from 'crypto';
import { SimulationConfig } from './types';

export default class SimulationController {
    private _devices: SmartMeterDevice[] = [];

    /**
     * Creates an instance of SimulationController.
     * @param {SimulationConfig} config The initial simulation configuration.
     * @memberof SimulationController
     */
    constructor(private readonly config: SimulationConfig) { }

    /**
     * @description Starts the simulation.
     * @memberof SimulationController
     */
    start() {
        const device = new SmartMeterDevice({
            deviceId: this.config.deviceId,
            deviceKey: this.config.deviceKey,
            idScope: this.config.idScope,
            gatewayName: this.config.gatewayName
        });

        this._registerEventListeners(device);
        this._devices.push(device);
        device.start();
    }

    private _registerEventListeners(device: SmartMeterDevice) {
        device.on(DeviceEvents.Registering, () => {
            console.log(`Registering device ${device.id} with scope ${device.idScope}`);
        });

        device.on(DeviceEvents.Registered, () => {
            console.log(`Registered device ${device.id}.`);
        });

        device.on(DeviceEvents.RegistrationError, (err) => {
            console.log(`Registration failed for device ${device.id}. Error: ${err}`);
        });

        device.on(DeviceEvents.Connecting, () => {
            console.log(`Connecting device ${device.id}`);
        });

        device.on(DeviceEvents.Connected, () => {
            console.log(`Connected device ${device.id}`);
        });

        device.on(DeviceEvents.ConnectionError, () => {
            console.log(`Connection failed for device ${device.id}`);
        });

        device.on(DeviceEvents.TelemetrySent, () => {
            console.log(`Sent telemetry for device ${device.id}`);
        });
    }
}

