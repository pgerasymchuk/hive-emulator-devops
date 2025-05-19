import { expect } from 'chai';
import axios from 'axios';
import { fetchCenterCoordinates, fetchHives, moveHives } from '../src/api/mapService.js';

const API = 'http://localhost:8080/api/v1/client';

describe('mapService', () => {
  const original = { get: axios.get, patch: axios.patch, error: console.error, log: console.log };
  let calls;

  beforeEach(() => {
    calls = [];
    console.error = () => {};
    console.log = () => {};
  });

  afterEach(() => {
    axios.get = original.get;
    axios.patch = original.patch;
    console.error = original.error;
    console.log = original.log;
  });

  const stubGet = (data) => {
    axios.get = async (url) => {
      calls.push(url);
      return { data };
    };
  };

  const failGet = () => {
    axios.get = async (url) => {
      calls.push(url);
      throw new Error('Network Error');
    };
  };

  describe('fetchCenterCoordinates', () => {
    it('requests the operational area and returns it', async () => {
      const area = { Latitude: 48.697189, Longitude: 38.066246, Radius_KM: 5 };
      stubGet(area);

      const result = await fetchCenterCoordinates(API);

      expect(calls).to.deep.equal([`${API}/area`]);
      expect(result).to.deep.equal(area);
    });

    it('returns null when the API is unreachable', async () => {
      failGet();

      expect(await fetchCenterCoordinates(API)).to.equal(null);
    });
  });

  describe('fetchHives', () => {
    it('maps hives to id/lat/lon and drops hives without telemetry', async () => {
      stubGet([
        { HiveID: '1', Telemetry: { Location: { Latitude: 48.7, Longitude: 38.1 } } },
        { HiveID: '2', Telemetry: null },
        { HiveID: '3' },
        { HiveID: '4', Telemetry: { Location: { Latitude: 0, Longitude: 0 } } },
      ]);

      const hives = await fetchHives(API);

      expect(calls).to.deep.equal([`${API}/hive`]);
      expect(hives).to.deep.equal([
        { id: '1', lat: 48.7, lon: 38.1 },
        { id: '4', lat: 0, lon: 0 },
      ]);
    });

    it('returns an empty list when the request fails', async () => {
      failGet();

      expect(await fetchHives(API)).to.deep.equal([]);
    });
  });

  describe('moveHives', () => {
    it('sends a PATCH with hive ids and destination', async () => {
      let request;
      axios.patch = async (url, body) => {
        request = { url, body };
        return { status: 202 };
      };

      await moveHives(API, 48.72, 38.09, ['1', '2']);

      expect(request).to.deep.equal({
        url: `${API}/hive`,
        body: { Hives: ['1', '2'], Destination: { Latitude: 48.72, Longitude: 38.09 } },
      });
    });

    it('rethrows errors so the map can report them', async () => {
      axios.patch = async () => {
        throw new Error('Request failed with status code 400');
      };

      let error;
      try {
        await moveHives(API, 0, 0, []);
      } catch (e) {
        error = e;
      }

      expect(error).to.be.an('error');
      expect(error.message).to.contain('400');
    });
  });
});
